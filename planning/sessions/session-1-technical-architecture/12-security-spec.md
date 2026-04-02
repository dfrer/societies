# Societies Security Specification

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [RPC Protocol](09-rpc-protocol.md) | [Technical Constants](../../meta/technical-constants.md)
>
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

**Status**: Draft  
**Last Updated**: 2026-02-01  
**Author**: Session 1 Architecture Team  
**Dependencies**: [09-rpc-protocol.md](09-rpc-protocol.md), [technical-constants.md](../../meta/technical-constants.md)

---

## 1. Security Architecture Overview

### 1.1 Core Principles

Societies implements a **server-authoritative security model** with zero trust for client input. All game state is calculated server-side; clients are thin rendering layers only.

```
┌─────────────────────────────────────────────────────────────┐
│                    SECURITY ARCHITECTURE                     │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐   │
│  │   Client    │────▶│   Server    │────▶│  Database   │   │
│  │  (Zero      │     │ (Authority) │     │  (Source)   │   │
│  │   Trust)    │◄────│  (Validate) │◄────│   of Truth  │   │
│  └─────────────┘     └─────────────┘     └─────────────┘   │
│        │                     │                              │
│        ▼                     ▼                              │
│  ┌─────────────┐     ┌─────────────┐                       │
│  │   Input     │     │   State     │                       │
│  │ Validation  │     │  Broadcast  │                       │
│  └─────────────┘     └─────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Defense in Depth

| Layer | Protection | Implementation |
|-------|-----------|----------------|
| 1 | **Transport** | ENet encryption (AES-256) on all channels |
| 2 | **Authentication** | JWT tokens with Redis session storage |
| 3 | **Authorization** | RBAC with 6 roles, 15+ permission types |
| 4 | **Input Validation** | Server-side validation of all client input |
| 5 | **Rate Limiting** | Per-IP and per-player action throttling |
| 6 | **Anti-Cheat** | Movement validation, inventory reconciliation |
| 7 | **Audit Logging** | Immutable security event logs |
| 8 | **Ban System** | 4-tier violation escalation |

### 1.3 Trust Model

```csharp
// NEVER trust client input
public void ProcessClientAction(Player player, ClientAction action) {
    // 1. Validate authentication
    if (!ValidateToken(player.SessionToken)) {
        Kick(player, "Invalid session");
        return;
    }
    
    // 2. Validate authorization
    if (!HasPermission(player, action.RequiredPermission)) {
        LogSecurityEvent(SecurityEventType.PermissionDenied, player, action.Type);
        return;
    }
    
    // 3. Validate input
    if (!InputValidator.IsValid(action, player)) {
        LogSecurityEvent(SecurityEventType.InvalidInput, player, action.Type);
        return;
    }
    
    // 4. Validate rate limits
    if (!RateLimiter.CanPerformAction(player, action.Type)) {
        LogSecurityEvent(SecurityEventType.RateLimitExceeded, player, action.Type);
        return;
    }
    
    // 5. Execute action server-side
    ExecuteAction(player, action);
    
    // 6. Broadcast result
    BroadcastActionResult(player, action);
}
```

---

## 2. Authentication System

### 2.1 Login Flow

```
┌─────────┐                                ┌─────────┐
│ Client  │                                │ Server  │
└────┬────┘                                └────┬────┘
     │                                          │
     │ 1. username + password_hash (SHA-256)    │
     │─────────────────────────────────────────▶│
     │                                          │
     │                    2. Verify credentials │
     │                    3. Create JWT token   │
     │                    4. Store in Redis     │
     │                                          │
     │ 5. JWT token (TTL: 24h)                  │
     │◀─────────────────────────────────────────│
     │                                          │
     │ 6. Include token in all RPC calls        │
     │══════════════════════════════════════════│
     │                                          │
```

### 2.2 JWT Token Structure

```csharp
// Header (Base64Url encoded)
{
  "alg": "HS256",
  "typ": "JWT"
}

// Payload (Base64Url encoded)
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",  // Player UUID
  "username": "player_name",
  "iat": 1704123456,                               // Issued at
  "exp": 1704209856,                               // Expires (24h)
  "world_id": "world_uuid",
  "roles": ["player", "citizen"],
  "jti": "unique_token_id"                         // Token ID for revocation
}

// Signature
HMAC-SHA256(
  base64UrlEncode(header) + "." + 
  base64UrlEncode(payload),
  SECRET_KEY
)
```

### 2.3 Token Validation

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

public class TokenValidator {
    private static readonly string SECRET_KEY = Environment.GetEnvironmentVariable("JWT_SECRET");
    private static readonly SymmetricSecurityKey _signingKey = 
        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SECRET_KEY));
    
    private static readonly TokenValidationParameters _validationParams = new() {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = true,
        ValidIssuer = "societies-server",
        ValidateAudience = true,
        ValidAudience = "societies-client",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5),  // 5 min tolerance for clock skew
        RequireSignedTokens = true,
        RequireExpirationTime = true
    };
    
    public bool ValidateToken(string token, out JwtSecurityToken validatedToken) {
        try {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, _validationParams, out SecurityToken securityToken);
            validatedToken = (JwtSecurityToken)securityToken;
            
            // Check Redis for revocation
            var tokenId = validatedToken.Claims.First(c => c.Type == "jti").Value;
            if (Redis.IsTokenRevoked(tokenId)) {
                validatedToken = null;
                return false;
            }
            
            return true;
        }
        catch (SecurityTokenExpiredException) {
            LogSecurityEvent(SecurityEventType.TokenExpired, null, "Token expired");
            validatedToken = null;
            return false;
        }
        catch (SecurityTokenInvalidSignatureException) {
            LogSecurityEvent(SecurityEventType.InvalidSignature, null, "Invalid signature");
            validatedToken = null;
            return false;
        }
        catch (Exception ex) {
            LogSecurityEvent(SecurityEventType.TokenValidationFailed, null, ex.Message);
            validatedToken = null;
            return false;
        }
    }
}
```

### 2.4 Redis Session Management

```csharp
public class SessionManager {
    private readonly IDatabase _redis;
    
    public async Task<string> CreateSession(Player player) {
        var token = GenerateJwtToken(player);
        var tokenId = ExtractTokenId(token);
        
        // Store session in Redis with 24h TTL
        var sessionData = new SessionData {
            PlayerId = player.Id,
            Username = player.Username,
            WorldId = player.WorldId,
            Roles = player.Roles,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };
        
        await _redis.StringSetAsync(
            $"session:{tokenId}",
            JsonSerializer.Serialize(sessionData),
            TimeSpan.FromHours(24)
        );
        
        // Track player sessions for logout-all capability
        await _redis.SetAddAsync($"player_sessions:{player.Id}", tokenId);
        
        return token;
    }
    
    public async Task<bool> RevokeSession(string tokenId) {
        await _redis.KeyDeleteAsync($"session:{tokenId}");
        await _redis.SetRemoveAsync($"player_sessions:{player.Id}", tokenId);
        return true;
    }
    
    public async Task RevokeAllPlayerSessions(string playerId) {
        var sessions = await _redis.SetMembersAsync($"player_sessions:{playerId}");
        foreach (var session in sessions) {
            await RevokeSession(session.ToString());
        }
    }
}
```

### 2.5 Integration with ENet RPC

Per [09-rpc-protocol.md](09-rpc-protocol.md), authentication uses Channel 0 (Critical) with reliable ordered delivery:

```csharp
// Authentication RPCs from 09-rpc-protocol.md
[RPC(TransferMode = TransferModeEnum.ReliableOrdered, Channel = 0, 
     Authority = MultiplayerAPI.RPCMode.AnyPeer)]
public void ServerAuthenticate(
    string username,                 // Max 32 chars
    string passwordHash,             // 64 bytes (SHA-256 hex)
    string clientVersion             // 20 bytes (semver)
);

[RPC(TransferMode = TransferModeEnum.ReliableOrdered, Channel = 0)]
public void ClientAuthenticationResult(
    bool success, 
    string token,                    // JWT token (variable, ~500 bytes)
    string errorMessage,             // Variable (max 100 chars)
    ServerInfo serverInfo            // ~80 bytes
);
```

---

## 3. Authorization (RBAC)

### 3.1 Role Hierarchy

```csharp
public enum UserRole {
    Guest = 0,          // Unauthenticated, spectate only
    Player = 1,         // Standard player, full gameplay
    VIP = 2,            // Supporter/backer, cosmetic perks
    Moderator = 3,      // Chat moderation, player assistance
    Admin = 4,          // Server administration, world management
    Developer = 5       // Full access, debugging, emergency powers
}

// Role inheritance
public static class RoleHierarchy {
    public static readonly Dictionary<UserRole, UserRole[]> RoleChain = new() {
        [UserRole.Guest] = new[] { UserRole.Guest },
        [UserRole.Player] = new[] { UserRole.Player, UserRole.Guest },
        [UserRole.VIP] = new[] { UserRole.VIP, UserRole.Player, UserRole.Guest },
        [UserRole.Moderator] = new[] { UserRole.Moderator, UserRole.Player, UserRole.Guest },
        [UserRole.Admin] = new[] { UserRole.Admin, UserRole.Moderator, UserRole.Player, UserRole.Guest },
        [UserRole.Developer] = new[] { UserRole.Developer, UserRole.Admin, UserRole.Moderator, UserRole.Player, UserRole.Guest }
    };
}
```

### 3.2 Permission System

```csharp
public static class Permissions {
    // World access
    public const string WorldJoin = "world:join";
    public const string WorldLeave = "world:leave";
    public const string WorldCreate = "world:create";           // Admin only
    public const string WorldDelete = "world:delete";           // Admin only
    
    // Building
    public const string BuildPlace = "build:place";
    public const string BuildDestroy = "build:destroy";
    public const string BuildAnywhere = "build:anywhere";       // Admin
    public const string BuildAdmin = "build:admin";             // Admin
    
    // Economy
    public const string Trade = "economy:trade";
    public const string StoreCreate = "economy:store:create";
    public const string StoreManage = "economy:store:manage";
    public const string EconomyAdmin = "economy:admin";         // Admin
    
    // Governance
    public const string Vote = "gov:vote";
    public const string ProposeLaw = "gov:propose";
    public const string CreateElection = "gov:election:create";
    public const string GovernanceAdmin = "gov:admin";          // Admin
    
    // Chat
    public const string ChatSend = "chat:send";
    public const string ChatModerate = "chat:moderate";
    public const string ChatAdmin = "chat:admin";               // Admin
    
    // Player management
    public const string Kick = "player:kick";                   // Moderator+
    public const string Ban = "player:ban";                     // Admin+
    public const string Mute = "player:mute";                   // Moderator+
    
    // System
    public const string ViewLogs = "system:logs";               // Admin+
    public const string ServerConfig = "system:config";         // Admin+
    public const string Emergency = "system:emergency";         // Developer
}

// Role-to-Permission mapping
public static class RolePermissions {
    public static readonly Dictionary<UserRole, string[]> Permissions = new() {
        [UserRole.Guest] = new string[] {
            // Read-only access
        },
        [UserRole.Player] = new string[] {
            Permissions.WorldJoin,
            Permissions.WorldLeave,
            Permissions.BuildPlace,
            Permissions.BuildDestroy,
            Permissions.Trade,
            Permissions.StoreCreate,
            Permissions.StoreManage,
            Permissions.Vote,
            Permissions.ProposeLaw,
            Permissions.ChatSend
        },
        [UserRole.VIP] = new string[] {
            // VIP inherits Player permissions + cosmetic
        },
        [UserRole.Moderator] = new string[] {
            Permissions.ChatModerate,
            Permissions.Kick,
            Permissions.Mute
        },
        [UserRole.Admin] = new string[] {
            Permissions.WorldCreate,
            Permissions.WorldDelete,
            Permissions.BuildAnywhere,
            Permissions.BuildAdmin,
            Permissions.EconomyAdmin,
            Permissions.GovernanceAdmin,
            Permissions.ChatAdmin,
            Permissions.Ban,
            Permissions.ViewLogs,
            Permissions.ServerConfig
        },
        [UserRole.Developer] = new string[] {
            Permissions.Emergency,
            Permissions.BuildAnywhere,
            Permissions.EconomyAdmin
            // + all admin permissions
        }
    };
}
```

### 3.3 Permission Checking

```csharp
public class AuthorizationManager {
    public bool HasPermission(Player player, string permission) {
        // Check each role in hierarchy
        foreach (var role in RoleHierarchy.RoleChain[player.PrimaryRole]) {
            if (RolePermissions.Permissions[role].Contains(permission)) {
                return true;
            }
        }
        
        // Check custom permissions
        if (player.CustomPermissions?.Contains(permission) == true) {
            return true;
        }
        
        return false;
    }
    
    public bool HasAnyPermission(Player player, params string[] permissions) {
        return permissions.Any(p => HasPermission(player, p));
    }
    
    public bool HasAllPermissions(Player player, params string[] permissions) {
        return permissions.All(p => HasPermission(player, p));
    }
    
    public void EnforcePermission(Player player, string permission, string action) {
        if (!HasPermission(player, permission)) {
            LogSecurityEvent(
                SecurityEventType.PermissionDenied,
                player,
                $"Attempted {action} without {permission}"
            );
            throw new UnauthorizedAccessException($"Missing permission: {permission}");
        }
    }
}

// Usage in RPC handlers
public void ServerCreateJurisdiction(JurisdictionCreateRequest request) {
    var player = GetPlayerFromPeer(Multiplayer.GetRemoteSenderId());
    
    _authorization.EnforcePermission(
        player, 
        Permissions.CreateElection,
        "create jurisdiction"
    );
    
    // Process jurisdiction creation...
}
```

### 3.4 Admin Command System

From [09-rpc-protocol.md](09-rpc-protocol.md), admin commands use Channel 10:

```csharp
[RPC(TransferMode = TransferModeEnum.ReliableOrdered, Channel = 10,
     Authority = MultiplayerAPI.RPCMode.AnyPeer)]
public void ServerAdminCommand(string command, string[] args) {
    var player = GetPlayerFromPeer(Multiplayer.GetRemoteSenderId());
    
    // Parse command
    var (baseCommand, requiredPermission) = ParseAdminCommand(command);
    
    // Verify permission
    if (!HasPermission(player, requiredPermission)) {
        ClientAdminResult(false, "Insufficient permissions", command);
        LogSecurityEvent(SecurityEventType.AdminCommandDenied, player, command);
        return;
    }
    
    // Log command execution
    LogSecurityEvent(SecurityEventType.AdminCommandExecuted, player, 
        $"{command} {string.Join(" ", args)}");
    
    // Execute command
    try {
        var result = ExecuteAdminCommand(baseCommand, args);
        ClientAdminResult(true, result, command);
    }
    catch (Exception ex) {
        ClientAdminResult(false, ex.Message, command);
    }
}
```

---

## 4. Input Validation

### 4.1 Validation Architecture

```csharp
public static class InputValidator {
    // Validation results
    public readonly record struct ValidationResult(bool IsValid, string ErrorMessage);
    
    // Validate position (anti-teleport, anti-speedhack)
    public static ValidationResult IsValidPosition(
        Vector3 newPosition, 
        Player player,
        double deltaTime) {
        
        var world = player.World;
        
        // Check world bounds
        // Reference: technical-constants.md WORLD_SIZE_MVP_KM2
        var maxCoord = MathF.Sqrt(TechnicalConstants.WORLD_SIZE_MVP_KM2) * 1000f; // 707m
        if (MathF.Abs(newPosition.X) > maxCoord || MathF.Abs(newPosition.Z) > maxCoord) {
            return new ValidationResult(false, "Position out of world bounds");
        }
        
        // Check altitude bounds
        if (newPosition.Y < -10f || newPosition.Y > 500f) {
            return new ValidationResult(false, "Position altitude out of bounds");
        }
        
        // Check movement speed (anti-speedhack)
        // Reference: technical-constants.md MOVEMENT_SPEED_SPRINT = 6.0f m/s
        var distance = Vector3.Distance(newPosition, player.LastPosition);
        var maxDistance = TechnicalConstants.MOVEMENT_SPEED_SPRINT * deltaTime;
        
        // Allow 50% margin for network jitter
        if (distance > maxDistance * 1.5f) {
            player.ViolationCount++;
            return new ValidationResult(false, 
                $"Speed violation: {distance / deltaTime:F2} m/s > {TechnicalConstants.MOVEMENT_SPEED_SPRINT:F2} m/s");
        }
        
        return new ValidationResult(true, null);
    }
    
    // Validate inventory operation
    public static ValidationResult IsValidInventoryOperation(
        Player player, 
        ItemStack items,
        InventoryOperationType operation) {
        
        // Check if player actually has these items
        if (operation == InventoryOperationType.Remove || 
            operation == InventoryOperationType.Transfer) {
            if (!player.Inventory.Contains(items)) {
                return new ValidationResult(false, "Player does not possess items");
            }
        }
        
        // Check stack size limits
        // Reference: technical-constants.md STACK_SIZE_* constants
        var maxStack = GetMaxStackSize(items.ItemId);
        if (items.Quantity > maxStack) {
            return new ValidationResult(false, $"Exceeds max stack size of {maxStack}");
        }
        
        // Check weight limits
        // Reference: technical-constants.md INVENTORY_WEIGHT_MAX_KG = 100.0f
        var newWeight = player.Inventory.TotalWeight + (items.Weight * items.Quantity);
        if (newWeight > TechnicalConstants.INVENTORY_WEIGHT_MAX_KG) {
            return new ValidationResult(false, "Exceeds inventory weight limit");
        }
        
        return new ValidationResult(true, null);
    }
    
    // Validate chat message
    public static ValidationResult IsValidChatMessage(Player player, string message) {
        // Length check
        if (string.IsNullOrWhiteSpace(message)) {
            return new ValidationResult(false, "Empty message");
        }
        
        if (message.Length > 500) {
            return new ValidationResult(false, "Message too long (max 500 chars)");
        }
        
        // Rate limiting check
        // Reference: Rate limiting section below
        if (player.ChatMessagesInLastMinute >= 30) {
            return new ValidationResult(false, "Chat rate limit exceeded");
        }
        
        return new ValidationResult(true, null);
    }
    
    // Validate trade
    public static ValidationResult IsValidTrade(Player buyer, Player seller, TradeRequest trade) {
        // Validate buyer has credits
        if (buyer.Credits < trade.BuyerCredits) {
            return new ValidationResult(false, "Buyer has insufficient credits");
        }
        
        // Validate seller has items
        foreach (var item in trade.SellerItems) {
            if (!seller.Inventory.Contains(item)) {
                return new ValidationResult(false, "Seller does not possess traded items");
            }
        }
        
        // Validate trade distance
        var distance = Vector3.Distance(buyer.Position, seller.Position);
        if (distance > 10f) { // Must be within 10 meters
            return new ValidationResult(false, "Players too far apart for trade");
        }
        
        return new ValidationResult(true, null);
    }
}
```

### 4.2 String Sanitization

```csharp
using System.Text.RegularExpressions;
using System.Web;

public static class InputSanitizer {
    // Remove dangerous characters
    public static string Sanitize(string input, int maxLength = 500) {
        if (string.IsNullOrEmpty(input)) {
            return string.Empty;
        }
        
        // Remove control characters
        input = Regex.Replace(input, "[\x00-\x1F\x7F-\x9F]", "");
        
        // Remove null bytes
        input = input.Replace("\0", "");
        
        // HTML encode to prevent XSS
        input = HttpUtility.HtmlEncode(input);
        
        // Trim whitespace
        input = input.Trim();
        
        // Enforce max length
        if (input.Length > maxLength) {
            input = input.Substring(0, maxLength);
        }
        
        return input;
    }
    
    // Sanitize username (stricter rules)
    public static string SanitizeUsername(string username) {
        if (string.IsNullOrEmpty(username)) {
            return null;
        }
        
        // Only allow alphanumeric, underscore, hyphen
        username = Regex.Replace(username, "[^a-zA-Z0-9_-]", "");
        
        // Length: 3-32 characters
        if (username.Length < 3 || username.Length > 32) {
            return null;
        }
        
        // Cannot start with number or special char
        if (char.IsDigit(username[0]) || username[0] == '-' || username[0] == '_') {
            return null;
        }
        
        return username.ToLowerInvariant();
    }
    
    // Sanitize command input
    public static string[] SanitizeCommandArgs(string[] args) {
        return args?.Select(a => Sanitize(a, 100)).ToArray() ?? Array.Empty<string>();
    }
}
```

---

## 5. Anti-Cheat Measures

### 5.1 Server-Authoritative Action Processing

```csharp
public class ActionProcessor {
    public void ProcessPlayerAction(Player player, PlayerAction action) {
        // Check if action is valid
        if (!IsValidActionType(action.Type)) {
            LogCheatAttempt(player, $"Invalid action type: {action.Type}");
            return;
        }
        
        // Check timing (prevent action spam)
        var timeSinceLastAction = DateTime.UtcNow - player.LastActionTime;
        if (timeSinceLastAction < GetMinActionInterval(action.Type)) {
            LogCheatAttempt(player, "Action spam detected");
            return;
        }
        
        // Verify resources/credits
        if (action.Cost > 0 && player.Credits < action.Cost) {
            LogCheatAttempt(player, $"Insufficient funds: {player.Credits} < {action.Cost}");
            return;
        }
        
        // Verify prerequisites
        if (!MeetsPrerequisites(player, action)) {
            LogCheatAttempt(player, "Prerequisites not met");
            return;
        }
        
        // All checks passed - execute
        ExecuteAction(player, action);
        player.LastActionTime = DateTime.UtcNow;
    }
    
    private TimeSpan GetMinActionInterval(PlayerActionType type) => type switch {
        PlayerActionType.Chat => TimeSpan.FromMilliseconds(500),
        PlayerActionType.Trade => TimeSpan.FromSeconds(1),
        PlayerActionType.Attack => TimeSpan.FromMilliseconds(500),
        PlayerActionType.Build => TimeSpan.FromMilliseconds(100),
        _ => TimeSpan.FromMilliseconds(100)
    };
}
```

### 5.2 Movement Validation (Anti-Speedhack)

```csharp
public class MovementValidator {
    // Reference: technical-constants.md
    private const float MAX_PLAYER_SPEED = 6.0f;  // MOVEMENT_SPEED_SPRINT
    private const float SPEED_TOLERANCE = 1.5f;    // 50% margin for lag
    
    public void ValidatePlayerMovement(Player player, Vector3 newPosition, double serverTime) {
        var timeDelta = serverTime - player.LastPositionTime;
        if (timeDelta <= 0) return; // Invalid timestamp
        
        var distance = Vector3.Distance(newPosition, player.LastPosition);
        var speed = distance / (float)timeDelta;
        var maxAllowedSpeed = MAX_PLAYER_SPEED * SPEED_TOLERANCE;
        
        if (speed > maxAllowedSpeed) {
            // Likely speed hack
            player.ViolationCount++;
            
            LogSecurityEvent(SecurityEventType.CheatDetected, player, 
                $"Speed hack: {speed:F2} m/s (max: {maxAllowedSpeed:F2} m/s)");
            
            if (player.ViolationCount > 3) {
                // Teleport back to last valid position
                TeleportPlayer(player, player.LastPosition);
                SendWarning(player, "Speed violation detected. Position corrected.");
            }
            
            if (player.ViolationCount > 10) {
                // Kick player
                KickPlayer(player, "Speed hacking detected");
            }
            
            return;
        }
        
        // Movement valid - update position
        player.LastPosition = player.Position;
        player.Position = newPosition;
        player.LastPositionTime = serverTime;
        
        // Decay violation count over time
        if (timeDelta > 5.0) {
            player.ViolationCount = Math.Max(0, player.ViolationCount - 1);
        }
    }
    
    public void TeleportPlayer(Player player, Vector3 position) {
        // Per 09-rpc-protocol.md - force position update
        player.Position = position;
        
        // Notify client of correction
        RpcId(player.PeerId, nameof(ClientPlayerMoveCorrection),
            player.Id,
            position,
            Vector3.Zero,  // velocity
            player.LastProcessedInput
        );
    }
}
```

### 5.3 Inventory Validation (Anti-Dupe)

```csharp
public class InventoryValidator {
    public void ValidateInventoryState(Player player) {
        // Recalculate inventory from server history
        var serverCalculatedInventory = RecalculateInventoryFromHistory(player);
        
        if (!serverCalculatedInventory.Equals(player.ClientReportedInventory)) {
            // Inventory desync detected - likely tampering
            var diff = serverCalculatedInventory.GetDifferences(player.ClientReportedInventory);
            
            LogSecurityEvent(SecurityEventType.CheatDetected, player,
                $"Inventory tampering: {diff}");
            
            // Force sync to server state
            player.Inventory = serverCalculatedInventory;
            player.ClientReportedInventory = null;
            
            // Send inventory update to client
            SendInventoryUpdate(player);
            
            // Increment violation
            player.ViolationCount++;
            
            if (player.ViolationCount > 5) {
                KickPlayer(player, "Inventory tampering detected");
            }
        }
    }
    
    private Inventory RecalculateInventoryFromHistory(Player player) {
        var inventory = new Inventory(player.Id);
        
        // Replay all inventory changes from server log
        var history = InventoryLog.GetPlayerHistory(player.Id);
        foreach (var change in history) {
            inventory.ApplyChange(change);
        }
        
        return inventory;
    }
}
```

### 5.4 Resource Gathering Validation

```csharp
public class ResourceGatheringValidator {
    public bool ValidateGatherAttempt(Player player, Guid resourceNodeId) {
        var node = World.GetResourceNode(resourceNodeId);
        
        if (node == null) {
            LogCheatAttempt(player, "Gather from non-existent node");
            return false;
        }
        
        // Check distance
        var distance = Vector3.Distance(player.Position, node.Position);
        if (distance > 5f) {
            LogCheatAttempt(player, $"Gather from too far: {distance:F2}m");
            return false;
        }
        
        // Check node state
        if (node.State != ResourceNodeState.Full) {
            LogCheatAttempt(player, "Gather from depleted node");
            return false;
        }
        
        // Check gathering cooldown
        if (player.LastGatherTime.TryGetValue(resourceNodeId, out var lastTime)) {
            var cooldown = GetGatherCooldown(node.Type);
            if (DateTime.UtcNow - lastTime < cooldown) {
                LogCheatAttempt(player, "Gather cooldown violation");
                return false;
            }
        }
        
        return true;
    }
}
```

---

## 6. Rate Limiting

### 6.1 Connection Rate Limiting (Anti-DDoS)

```csharp
public class ConnectionRateLimiter {
    private readonly Dictionary<string, ConnectionInfo> _connections = new();
    private readonly object _lock = new();
    
    private class ConnectionInfo {
        public int AttemptsInLastMinute;
        public DateTime LastAttempt;
        public DateTime WindowStart;
    }
    
    public bool CanConnect(string ipAddress) {
        lock (_lock) {
            var now = DateTime.UtcNow;
            
            if (!_connections.TryGetValue(ipAddress, out var info)) {
                _connections[ipAddress] = new ConnectionInfo {
                    AttemptsInLastMinute = 1,
                    LastAttempt = now,
                    WindowStart = now
                };
                return true;
            }
            
            // Reset window if minute has passed
            if (now - info.WindowStart > TimeSpan.FromMinutes(1)) {
                info.AttemptsInLastMinute = 0;
                info.WindowStart = now;
            }
            
            // Max 5 connections per IP per minute
            if (info.AttemptsInLastMinute >= 5) {
                LogSecurityEvent(SecurityEventType.RateLimitExceeded, null,
                    $"Connection rate limit exceeded for {ipAddress}");
                return false;
            }
            
            info.AttemptsInLastMinute++;
            info.LastAttempt = now;
            return true;
        }
    }
}
```

### 6.2 Action Rate Limiting

```csharp
public class ActionRateLimiter {
    private readonly Dictionary<string, RateLimitInfo> _actionCounts = new();
    private readonly object _lock = new();
    
    private class RateLimitInfo {
        public int Count;
        public DateTime WindowStart;
        public DateTime LastAction;
    }
    
    // Limits per action type (actions per minute)
    private static readonly Dictionary<string, int> _limits = new() {
        ["chat"] = 30,
        ["trade"] = 10,
        ["build"] = 60,
        ["gather"] = 120,
        ["attack"] = 120,
        ["move"] = int.MaxValue,  // Movement handled separately
        ["default"] = 100
    };
    
    public bool CanPerformAction(Player player, string actionType) {
        var key = $"{player.Id}:{actionType}";
        var limit = _limits.GetValueOrDefault(actionType, _limits["default"]);
        
        lock (_lock) {
            var now = DateTime.UtcNow;
            
            if (!_actionCounts.TryGetValue(key, out var info)) {
                _actionCounts[key] = new RateLimitInfo {
                    Count = 1,
                    WindowStart = now,
                    LastAction = now
                };
                return true;
            }
            
            // Reset window if minute has passed
            if (now - info.WindowStart > TimeSpan.FromMinutes(1)) {
                info.Count = 0;
                info.WindowStart = now;
            }
            
            if (info.Count >= limit) {
                LogSecurityEvent(SecurityEventType.RateLimitExceeded, player,
                    $"Action rate limit exceeded: {actionType}");
                
                // Notify client
                ClientRateLimited(actionType, 60);
                return false;
            }
            
            info.Count++;
            info.LastAction = now;
            return true;
        }
    }
    
    // Cleanup old entries periodically
    public void Cleanup() {
        lock (_lock) {
            var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(2);
            var oldKeys = _actionCounts
                .Where(kvp => kvp.Value.LastAction < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in oldKeys) {
                _actionCounts.Remove(key);
            }
        }
    }
}
```

---

## 7. Encryption

### 7.1 Transport Encryption (ENet)

```csharp
public class NetworkEncryption {
    private byte[] _serverPublicKey;
    private byte[] _serverPrivateKey;
    
    public void Initialize() {
        // Generate or load encryption keys
        var keys = LoadOrGenerateKeys();
        _serverPublicKey = keys.PublicKey;
        _serverPrivateKey = keys.PrivateKey;
    }
    
    public void ConfigureHost(ENetHost host) {
        // Enable ENet encryption (AES-256-GCM)
        // Note: ENet encryption support varies by Godot version
        // This is conceptual implementation
        
        // Generate session key for each connection
        host.OnPeerConnect += (peer) => {
            var sessionKey = GenerateSessionKey();
            peer.SetEncryptionKey(sessionKey);
            
            // Send public key to client during handshake
            // Client derives shared secret using ECDH
        };
    }
    
    private byte[] GenerateSessionKey() {
        // Generate 256-bit AES key
        using var rng = RandomNumberGenerator.Create();
        var key = new byte[32];
        rng.GetBytes(key);
        return key;
    }
}

// Alternative: TLS-like handshake for custom implementation
public class SecureHandshake {
    public byte[] PerformHandshake(ENetPeer peer) {
        // 1. Server sends public key + nonce
        var serverPublicKey = GetServerPublicKey();
        var serverNonce = GenerateNonce();
        SendToPeer(peer, serverPublicKey, serverNonce);
        
        // 2. Client sends encrypted session key (using server's public key)
        var encryptedSessionKey = ReceiveFromPeer(peer);
        var sessionKey = DecryptWithPrivateKey(encryptedSessionKey);
        
        // 3. All subsequent communication encrypted with session key
        return sessionKey;
    }
}
```

### 7.2 Database Encryption

```csharp
public class DatabaseEncryption {
    private readonly IEncryptionService _encryption;
    
    public DatabaseEncryption(IEncryptionService encryption) {
        _encryption = encryption;
    }
    
    // Encrypt sensitive fields before storage
    public PlayerEntity EncryptPlayerData(PlayerEntity player) {
        return player with {
            Email = _encryption.Encrypt(player.Email),
            PhoneNumber = player.PhoneNumber != null 
                ? _encryption.Encrypt(player.PhoneNumber) 
                : null,
            // Password hash is already hashed, but we can add layer of encryption
            PasswordHash = _encryption.Encrypt(player.PasswordHash)
        };
    }
    
    public PlayerEntity DecryptPlayerData(PlayerEntity player) {
        return player with {
            Email = _encryption.Decrypt(player.Email),
            PhoneNumber = player.PhoneNumber != null 
                ? _encryption.Decrypt(player.PhoneNumber) 
                : null,
            PasswordHash = _encryption.Decrypt(player.PasswordHash)
        };
    }
}

// AES-256-GCM implementation
public class AesEncryptionService : IEncryptionService {
    private readonly byte[] _key;
    
    public AesEncryptionService(string keyBase64) {
        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32) {
            throw new ArgumentException("Key must be 256 bits (32 bytes)");
        }
    }
    
    public string Encrypt(string plaintext) {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        
        return Convert.ToBase64String(result);
    }
    
    public string Decrypt(string ciphertext) {
        var fullCipher = Convert.FromBase64String(ciphertext);
        
        using var aes = Aes.Create();
        aes.Key = _key;
        
        // Extract IV
        var iv = new byte[16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        aes.IV = iv;
        
        // Decrypt
        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 16, cipherBytes, 0, cipherBytes.Length);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

---

## 8. Audit Logging

### 8.1 Security Event Types

```csharp
public enum SecurityEventType {
    // Authentication events
    LoginSuccess,
    LoginFailure,
    Logout,
    TokenExpired,
    TokenValidationFailed,
    InvalidSignature,
    SessionRevoked,
    
    // Authorization events
    PermissionDenied,
    AdminCommandExecuted,
    AdminCommandDenied,
    
    // Rate limiting
    RateLimitExceeded,
    ConnectionThrottled,
    
    // Anti-cheat
    CheatDetected,
    SpeedViolation,
    InventoryTampering,
    InvalidInput,
    
    // Player management
    PlayerKicked,
    PlayerBanned,
    PlayerMuted,
    PlayerWarned,
    
    // System
    ConfigurationChange,
    DataAccess,
    BackupCreated,
    EmergencyAction
}

public enum Severity {
    Info,
    Warning,
    Error,
    Critical
}
```

### 8.2 Audit Logger

```csharp
public class SecurityAuditLogger {
    private readonly ILogger<SecurityAuditLogger> _logger;
    private readonly IDatabase _db;
    private readonly IAlertService _alerts;
    
    public async void LogSecurityEvent(
        SecurityEventType eventType, 
        Player player, 
        string details,
        Severity? severity = null) {
        
        var sev = severity ?? GetSeverity(eventType);
        
        var logEntry = new SecurityLogEntry {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Severity = sev,
            PlayerId = player?.Id,
            Username = player?.Username,
            IPAddress = player?.IPAddress,
            WorldId = player?.WorldId,
            Details = details,
            SessionId = player?.SessionToken
        };
        
        // Write to database (hot storage)
        await _db.SecurityLogs.InsertAsync(logEntry);
        
        // Structured logging
        _logger.Log(
            MapToLogLevel(sev),
            "Security event: {EventType} - Player: {PlayerId} - Details: {Details}",
            eventType,
            player?.Id ?? "N/A",
            details
        );
        
        // Alert on critical events
        if (sev == Severity.Critical) {
            await _alerts.SendAlert(new SecurityAlert {
                Title = $"Critical Security Event: {eventType}",
                Message = $"Player: {player?.Username ?? "N/A"} - {details}",
                Timestamp = DateTime.UtcNow,
                EventId = logEntry.Id
            });
        }
    }
    
    private Severity GetSeverity(SecurityEventType type) => type switch {
        SecurityEventType.LoginSuccess => Severity.Info,
        SecurityEventType.Logout => Severity.Info,
        SecurityEventType.LoginFailure => Severity.Warning,
        SecurityEventType.PermissionDenied => Severity.Warning,
        SecurityEventType.RateLimitExceeded => Severity.Warning,
        SecurityEventType.CheatDetected => Severity.Error,
        SecurityEventType.InventoryTampering => Severity.Error,
        SecurityEventType.AdminCommandExecuted => Severity.Info,
        _ => Severity.Warning
    };
}

public class SecurityLogEntry {
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public SecurityEventType EventType { get; set; }
    public Severity Severity { get; set; }
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public string IPAddress { get; set; }
    public string WorldId { get; set; }
    public string Details { get; set; }
    public string SessionId { get; set; }
}
```

### 8.3 Audit Retention

```sql
-- Hot storage: 90 days
CREATE TABLE security_logs_hot (
    id UUID PRIMARY KEY,
    timestamp TIMESTAMP NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    severity VARCHAR(20) NOT NULL,
    player_id VARCHAR(36),
    username VARCHAR(32),
    ip_address INET,
    details TEXT,
    INDEX idx_timestamp (timestamp),
    INDEX idx_player (player_id),
    INDEX idx_event_type (event_type)
);

-- Cold storage: 2 years (partitioned by month)
CREATE TABLE security_logs_cold (
    LIKE security_logs_hot INCLUDING ALL
) PARTITION BY RANGE (timestamp);

-- Automated archiving
CREATE EVENT archive_security_logs
ON SCHEDULE EVERY 1 DAY
DO
    INSERT INTO security_logs_cold 
    SELECT * FROM security_logs_hot 
    WHERE timestamp < DATE_SUB(NOW(), INTERVAL 90 DAY);
    
    DELETE FROM security_logs_hot 
    WHERE timestamp < DATE_SUB(NOW(), INTERVAL 90 DAY);
```

---

## 9. Ban/Kick System

### 9.1 Violation Levels

```csharp
public enum ViolationLevel {
    Level1,  // Warning
    Level2,  // Kick
    Level3,  // Temp Ban
    Level4   // Permanent Ban
}

public class ViolationPolicy {
    public static readonly Dictionary<ViolationLevel, ViolationAction> Actions = new() {
        [ViolationLevel.Level1] = new ViolationAction {
            Name = "Warning",
            Action = (player, reason) => {
                SendWarning(player, $"Warning: {reason}");
                player.WarningCount++;
            },
            Duration = TimeSpan.Zero
        },
        
        [ViolationLevel.Level2] = new ViolationAction {
            Name = "Kick",
            Action = (player, reason) => {
                KickPlayer(player, $"Kicked: {reason}");
            },
            Duration = TimeSpan.Zero
        },
        
        [ViolationLevel.Level3] = new ViolationAction {
            Name = "Temporary Ban",
            Action = (player, reason) => {
                BanPlayer(player, BanType.Temporary, reason, TimeSpan.FromDays(7));
            },
            Duration = TimeSpan.FromDays(7),
            CanAppeal = true
        },
        
        [ViolationLevel.Level4] = new ViolationAction {
            Name = "Permanent Ban",
            Action = (player, reason) => {
                BanPlayer(player, BanType.Permanent, reason, null);
            },
            Duration = TimeSpan.MaxValue,
            CanAppeal = true,
            AppealCooldown = TimeSpan.FromDays(30)
        }
    };
}
```

### 9.2 Ban Implementation

```csharp
public class BanManager {
    private readonly IDatabase _db;
    private readonly ICache _cache;
    
    public async Task BanPlayer(
        Player player, 
        BanType type, 
        string reason, 
        TimeSpan? duration,
        Player admin = null) {
        
        var ban = new Ban {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Username = player.Username,
            IPAddress = player.IPAddress,
            HardwareId = player.HardwareId,
            Type = type,
            Reason = reason,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = duration.HasValue 
                ? DateTime.UtcNow + duration.Value 
                : DateTime.MaxValue,
            IssuedBy = admin?.Id ?? "SYSTEM",
            IssuedByUsername = admin?.Username ?? "System"
        };
        
        // Persist ban
        await _db.Bans.InsertAsync(ban);
        
        // Cache for fast lookup
        await _cache.SetAsync($"ban:{player.Id}", ban, duration ?? TimeSpan.FromDays(3650));
        await _cache.SetAsync($"ban_ip:{player.IPAddress}", ban, duration ?? TimeSpan.FromDays(3650));
        
        // Log event
        LogSecurityEvent(SecurityEventType.PlayerBanned, player, 
            $"Type: {type}, Reason: {reason}, Duration: {duration?.ToString() ?? "Permanent"}, By: {ban.IssuedByUsername}");
        
        // Kick if online
        if (player.IsOnline) {
            KickPlayer(player, $"Banned: {reason}");
        }
        
        // Revoke all sessions
        await SessionManager.RevokeAllPlayerSessions(player.Id);
    }
    
    public async Task<bool> IsBanned(string playerId, string ipAddress) {
        // Check cache first
        var cachedBan = await _cache.GetAsync<Ban>($"ban:{playerId}");
        if (cachedBan != null && cachedBan.ExpiresAt > DateTime.UtcNow) {
            return true;
        }
        
        var cachedIpBan = await _cache.GetAsync<Ban>($"ban_ip:{ipAddress}");
        if (cachedIpBan != null && cachedIpBan.ExpiresAt > DateTime.UtcNow) {
            return true;
        }
        
        // Check database
        var activeBan = await _db.Bans
            .Where(b => (b.PlayerId == playerId || b.IPAddress == ipAddress) 
                        && b.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        
        if (activeBan != null) {
            // Refresh cache
            await _cache.SetAsync($"ban:{playerId}", activeBan, 
                activeBan.ExpiresAt - DateTime.UtcNow);
            return true;
        }
        
        return false;
    }
    
    public async Task UnbanPlayer(string playerId, Player admin) {
        var ban = await _db.Bans
            .Where(b => b.PlayerId == playerId && b.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        
        if (ban != null) {
            ban.ExpiresAt = DateTime.UtcNow;
            ban.RevokedAt = DateTime.UtcNow;
            ban.RevokedBy = admin?.Id;
            
            await _db.Bans.UpdateAsync(ban);
            await _cache.DeleteAsync($"ban:{playerId}");
            
            LogSecurityEvent(SecurityEventType.BanRevoked, null,
                $"Player: {playerId}, Revoked by: {admin?.Username}");
        }
    }
}

public enum BanType {
    Temporary,
    Permanent
}

public class Ban {
    public Guid Id { get; set; }
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public string IPAddress { get; set; }
    public string HardwareId { get; set; }
    public BanType Type { get; set; }
    public string Reason { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string IssuedBy { get; set; }
    public string IssuedByUsername { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string RevokedBy { get; set; }
}
```

### 9.3 Ban RPCs

From [09-rpc-protocol.md](09-rpc-protocol.md):

```csharp
[RPC(TransferMode = TransferModeEnum.ReliableOrdered, Channel = 10)]
public void ClientBanned(
    string reason,                   // Variable
    int durationMinutes,             // 4 bytes
    string bannedBy                  // Variable
);
```

---

## 10. Secure Configuration

### 10.1 Secrets Management

```csharp
public class SecretsManager {
    // NEVER hardcode secrets
    // Use environment variables for development
    // Use proper secret management for production
    
    public static string GetSecret(string key) {
        // 1. Try environment variable
        var envValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(envValue)) {
            return envValue;
        }
        
        // 2. Try Docker secrets (production)
        var dockerSecretPath = $"/run/secrets/{key.ToLowerInvariant()}";
        if (File.Exists(dockerSecretPath)) {
            return File.ReadAllText(dockerSecretPath).Trim();
        }
        
        // 3. Try AWS Secrets Manager (production AWS)
        if (IsAwsEnvironment()) {
            return GetAwsSecret(key);
        }
        
        // 4. Try Azure Key Vault (production Azure)
        if (IsAzureEnvironment()) {
            return GetAzureSecret(key);
        }
        
        throw new InvalidOperationException($"Secret not found: {key}");
    }
    
    public static void ValidateSecrets() {
        var requiredSecrets = new[] {
            "JWT_SECRET",
            "DB_PASSWORD",
            "ENCRYPTION_KEY",
            "REDIS_PASSWORD"
        };
        
        foreach (var secret in requiredSecrets) {
            var value = GetSecret(secret);
            
            if (string.IsNullOrEmpty(value)) {
                throw new ConfigurationException($"Required secret is empty: {secret}");
            }
            
            // Check minimum length for cryptographic secrets
            if (secret.Contains("SECRET") || secret.Contains("KEY")) {
                if (value.Length < 32) {
                    throw new ConfigurationException(
                        $"Secret {secret} must be at least 32 characters");
                }
            }
            
            // Check for default/weak passwords
            var weakPasswords = new[] { "password", "admin", "123456", "secret", "default" };
            if (weakPasswords.Any(w => value.ToLowerInvariant().Contains(w))) {
                throw new ConfigurationException(
                    $"Secret {secret} appears to use a weak/default value");
            }
        }
    }
}
```

### 10.2 Configuration Validation

```csharp
public class SecurityConfigurationValidator {
    public void ValidateConfiguration() {
        // Validate JWT configuration
        var jwtSecret = SecretsManager.GetSecret("JWT_SECRET");
        if (jwtSecret.Length < 32) {
            throw new ConfigurationException("JWT_SECRET must be at least 32 characters");
        }
        
        // Validate database configuration
        var dbPassword = SecretsManager.GetSecret("DB_PASSWORD");
        if (string.IsNullOrEmpty(dbPassword)) {
            throw new ConfigurationException("DB_PASSWORD not configured");
        }
        
        // Validate encryption key
        var encryptionKey = SecretsManager.GetSecret("ENCRYPTION_KEY");
        if (Convert.FromBase64String(encryptionKey).Length != 32) {
            throw new ConfigurationException("ENCRYPTION_KEY must be 256 bits (32 bytes)");
        }
        
        // Validate rate limiting configuration
        if (TechnicalConstants.TICK_RATE < 10 || TechnicalConstants.TICK_RATE > 60) {
            throw new ConfigurationException("TICK_RATE out of valid range (10-60)");
        }
        
        // Validate world size
        if (TechnicalConstants.WORLD_SIZE_MVP_KM2 < 0.1f || 
            TechnicalConstants.WORLD_SIZE_MVP_KM2 > 4.0f) {
            throw new ConfigurationException("WORLD_SIZE_MVP_KM2 out of valid range");
        }
        
        // Log successful validation
        Console.WriteLine("Security configuration validated successfully");
    }
}
```

### 10.3 Development vs Production

```csharp
public class EnvironmentConfig {
    public static bool IsDevelopment => 
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    
    public static bool IsProduction => 
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    
    public static void ApplyEnvironmentSettings() {
        if (IsDevelopment) {
            // Development: Allow relaxed settings
            // - Detailed error messages
            // - Debug logging
            // - Swagger/OpenAPI enabled
        }
        
        if (IsProduction) {
            // Production: Strict security
            // - Require all secrets
            // - Enforce rate limits strictly
            // - Enable audit logging
            // - Disable debug endpoints
            
            SecretsManager.ValidateSecrets();
            SecurityConfigurationValidator.ValidateConfiguration();
        }
    }
}
```

---

## 11. Integration with Existing Systems

### 11.1 RPC Security Integration

Per [09-rpc-protocol.md](09-rpc-protocol.md), security checks are integrated at RPC entry points:

```csharp
// Example: All player action RPCs include security checks
[RPC(TransferMode = TransferModeEnum.ReliableOrdered, Channel = 0,
     Authority = MultiplayerAPI.RPCMode.AnyPeer)]
public void ServerPlayerAction(Guid playerId, PlayerActionType action, ActionData data) {
    var peerId = Multiplayer.GetRemoteSenderId();
    var player = AuthenticatePlayer(peerId);
    
    if (player == null) {
        ClientDisconnected(DisconnectReason.AuthFailed, "Authentication required");
        return;
    }
    
    if (!RateLimiter.CanPerformAction(player, action.ToString())) {
        ClientRateLimited(action.ToString(), 60);
        return;
    }
    
    if (!InputValidator.IsValidAction(player, action, data).IsValid) {
        LogSecurityEvent(SecurityEventType.InvalidInput, player, action.ToString());
        return;
    }
    
    // Process action...
}
```

### 11.2 Constants Integration

Reference [technical-constants.md](../../meta/technical-constants.md) for security-related values:

```csharp
// Movement validation
var maxSpeed = TechnicalConstants.MOVEMENT_SPEED_SPRINT;  // 6.0f m/s

// Position validation
var worldSize = MathF.Sqrt(TechnicalConstants.WORLD_SIZE_MVP_KM2) * 1000f;

// Inventory validation
var maxWeight = TechnicalConstants.INVENTORY_WEIGHT_MAX_KG;  // 100.0f
var maxSlots = TechnicalConstants.INVENTORY_SLOTS_PLAYER;    // 64

// Network timing
var tickRate = TechnicalConstants.TICK_RATE;  // 20 TPS
var tickInterval = TechnicalConstants.TICK_INTERVAL_MS;  // 50ms
```

---

## 12. Security Checklist

### Pre-Deployment

- [ ] All secrets stored in environment variables or secret manager
- [ ] JWT secret is 32+ characters, randomly generated
- [ ] Database passwords are strong (not default/weak)
- [ ] Encryption keys are 256-bit (32 bytes)
- [ ] Redis authentication enabled
- [ ] Rate limiting configured and tested
- [ ] Audit logging enabled
- [ ] Anti-cheat measures active
- [ ] Ban system configured
- [ ] Security event alerting configured

### Ongoing

- [ ] Review security logs daily
- [ ] Monitor for unusual patterns
- [ ] Rotate secrets quarterly
- [ ] Update dependencies for security patches
- [ ] Review access logs monthly
- [ ] Test backup/restore procedures
- [ ] Conduct security audits annually

---

## 13. References

- [09-rpc-protocol.md](09-rpc-protocol.md) - RPC protocol specification
- [technical-constants.md](../../meta/technical-constants.md) - All numerical constants
- [02-client-server-architecture.md](02-client-server-architecture.md) - Network architecture
- [03-data-persistence.md](03-data-persistence.md) - Database security context

---

**END OF DOCUMENT**

*This security specification is part of the Session 1 Technical Architecture planning. All implementations must follow the server-authoritative, zero-trust principles outlined herein.*
