# 3D Jurisdiction and Land Claims Specification

> **Navigation**: [Session 5 Index]([AGENTS-READ-FIRST]-index.md) | [Main Governance Document](./day5-governance-mechanics.md)
> 
> **Part of**: Session 5 - Governance Mechanics
> **Related**: [Session 1 Voxel World System](../session-1-technical-architecture/13-voxel-world-system.md)

---

**Planning Session**: 5 of 7  
**Status**: 📝 Draft  
**Date Started**: 2026-02-01  
**Date Completed**: TBD  
**Document Type**: Technical Specification

> **Canonical alignment (2026-07-14):** Aspirational jurisdiction reference. Current scope is [planning/active/](../../active/) and implementation truth is [CURRENT_BUILD.md](../../../CURRENT_BUILD.md). See [PRODUCT-THESIS.md](../../PRODUCT-THESIS.md).

## Product Contract Alignment

Jurisdiction and property outcomes are deterministic facts produced by validated civic commands and recorded events. Rights-bearing AI citizens do not gain special authority; LLMs may explain or propose from structured observations but cannot mutate claims, and invalid output safely falls back.

---

## Executive Summary

This document specifies the complete 3D land claim and jurisdiction system for Societies, extending traditional 2D territory concepts into full volumetric space. In a voxel world where vertical depth spans 256 meters (Y=-200 to Y=+56), property rights must account for the full three-dimensional nature of land ownership.

**Core Philosophy**: Property rights in Societies mirror real-world property law where ownership extends from the heavens to the depths ("ad coelum et ad inferos" doctrine), but with practical limitations and special provisions for the unique characteristics of a voxel civilization simulation.

**Why Vertical Space Matters**:
- **Subsurface Resources**: Mining and geological extraction occur at specific depths
- **Air Rights**: Building height affects skyline, views, and construction density
- **Underground Infrastructure**: Tunnels, utilities, and subterranean cities require clear jurisdiction
- **Layered Ownership**: Different parties may own surface, subsurface, and air rights independently

**Real-World Parallels**:
- **Mineral Rights** (US/Canada): Surface and subsurface ownership can be separated
- **Air Rights** (NYC/London): Development rights above properties are tradable commodities
- **Strata Title** (Australia/SE Asia): Vertical subdivision of buildings into separately owned units
- **Easements**: Rights to cross others' property for utilities or access
- **Ad Coelum Doctrine**: Traditional common law principle (limited in modern practice)

---

## 1. 3D Coordinate System

### 1.1 World Coordinate Specification

Societies uses a right-handed 3D coordinate system aligned with the voxel grid:

| Axis | Direction | Range (MVP) | Unit | Notes |
|------|-----------|-------------|------|-------|
| **X** | East (+) / West (-) | -352 to +351 | meters | Horizontal, chunk-aligned |
| **Y** | Up (+) / Down (-) | -200 to +56 | meters | Vertical, geological depth |
| **Z** | South (+) / North (-) | -352 to +351 | meters | Horizontal, chunk-aligned |

**Y-Axis Orientation**:
```
Y = +56  ━━━━━━━━━━ World Ceiling (air only, no building)
         
Y = 0    ━━━━━━━━━━ Sea Level / Surface Reference
         
Y = -200 ━━━━━━━━━━ Bedrock Layer (unmodifiable)
```

### 1.2 Claim Boundary Representation

Claims are defined as 3D axis-aligned bounding boxes (AABB):

```csharp
public struct ClaimBounds3D
{
    public int MinX;  // Inclusive western boundary
    public int MaxX;  // Inclusive eastern boundary
    public int MinY;  // Inclusive bottom boundary (depth)
    public int MaxY;  // Inclusive top boundary (height)
    public int MinZ;  // Inclusive northern boundary
    public int MaxZ;  // Inclusive southern boundary
    
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public int Depth => MaxZ - MinZ + 1;
    public int Volume => Width * Height * Depth;
    
    public bool Contains(int x, int y, int z) =>
        x >= MinX && x <= MaxX &&
        y >= MinY && y <= MaxY &&
        z >= MinZ && z <= MaxZ;
        
    public bool Intersects(ClaimBounds3D other) =>
        MinX <= other.MaxX && MaxX >= other.MinX &&
        MinY <= other.MaxY && MaxY >= other.MinY &&
        MinZ <= other.MaxZ && MaxZ >= other.MinZ;
}
```

### 1.3 Chunk-Based vs Block-Based Claims

**Claim Granularity Options**:

| Granularity | Unit Size | Precision | Use Case | Performance |
|-------------|-----------|-----------|----------|-------------|
| **Block-Level** | 1m³ | High | Personal homes, precise builds | Higher overhead |
| **Chunk-Level** | 16×16×256 | Low | Towns, large zones | Optimized queries |
| **Hybrid** | Variable | Configurable | General purpose | Balanced |

**Hybrid System Implementation**:
```csharp
public class Claim3D
{
    // Primary bounds (block-level precision for X/Z, Y always block-level)
    public ClaimBounds3D Bounds { get; set; }
    
    // Chunk coverage for fast lookup
    public List<Vector2I> CoveredChunks { get; set; }
    
    // Claim type determines default vertical extent
    public ClaimType Type { get; set; }
    
    // Optional: Sub-claims for 3D subdivision
    public List<SubClaim3D> SubClaims { get; set; }
}
```

### 1.4 Vertical Extent Terminology

**Height/Depth Definitions**:

| Term | Y Range | Description |
|------|---------|-------------|
| **Sky/Stratosphere** | +40 to +56 | Reserved airspace, restricted construction |
| **Air Rights Zone** | +20 to +40 | Constructible airspace above claims |
| **Building Height** | 0 to +20 | Standard construction zone |
| **Surface** | -5 to 0 | Ground level, topsoil, vegetation |
| **Subsurface** | -30 to -5 | Shallow underground, utilities, basements |
| **Deep Subsurface** | -80 to -30 | Mining zone, geological resources |
| **Bedrock Layer** | -200 to -80 | Deep mining, rare resources, unmodifiable base |

---

## 2. Claim Types and Dimensions

### 2.1 Personal Claims

**Small-scale individual property**:

| Parameter | Value | Notes |
|-----------|-------|-------|
| Max Horizontal Area | 32×32 blocks (1,024 m²) | Prevents land hoarding |
| Default Vertical Extent | Y=-30 to Y=+20 | Full mining + building rights |
| Min/Max Height | Configurable | Owner can adjust within limits |
| Claim Cost | Volume-based | Credits per cubic meter |
| Overlap Allowed | No | Exclusive personal property |

```csharp
public class PersonalClaim3D : Claim3D
{
    public override ClaimType Type => ClaimType.Personal;
    
    // Personal claims default to surface + building + shallow mining
    public static readonly int DefaultMinY = -30;
    public static readonly int DefaultMaxY = +20;
    public static readonly int MaxHorizontalArea = 1024; // 32×32
    
    // Owner can extend vertically (with cost)
    public bool CanExtendDepth => true;
    public bool CanExtendHeight => true;
    public int MaxExtensionDepth = -200;  // To bedrock
    public int MaxExtensionHeight = +40;  // To air rights limit
}
```

### 2.2 Town/City Claims

**Larger 3D volumes for settlements**:

| Parameter | Small Town | City | Metropolis |
|-----------|------------|------|------------|
| Horizontal Area | 96×96 (9,216 m²) | 160×160 (25,600 m²) | 256×256 (65,536 m²) |
| Default Y Range | -50 to +30 | -80 to +40 | -100 to +50 |
| Volume | ~1.6M m³ | ~9.2M m³ | ~38M m³ |
| Governance | Town council | Mayor + Council | Multi-district |
| Subdivision | Parcels | Zones + Parcels | Districts + Zones |

**Town Claim Structure**:
```csharp
public class TownClaim3D : Claim3D
{
    public override ClaimType Type => ClaimType.Town;
    
    // Town claims support parcel subdivision
    public List<Parcel3D> Parcels { get; set; }
    
    // Towns have defined zoning layers
    public ZoningProfile3D Zoning { get; set; }
    
    // Public infrastructure zone (always included)
    public ClaimBounds3D PublicZone { get; set; }
    
    // Methods for parcel management
    public Parcel3D CreateParcel(ClaimBounds3D bounds, Citizen owner);
    public bool CanSubdivide(ClaimBounds3D proposedBounds);
}
```

### 2.3 Industrial Zones

**Specialized 3D zones for resource extraction and manufacturing**:

| Zone Type | Y Range | Purpose | Restrictions |
|-----------|---------|---------|--------------|
| **Surface Industrial** | -5 to +20 | Factories, processing | No residential |
| **Mining Zone** | -200 to -10 | Extractive operations | Permit required |
| **Underground Complex** | -100 to -30 | Subterranean industry | Ventilation requirements |
| **Sky Platform** | +30 to +50 | Airborne structures | View impact review |

```csharp
public class IndustrialZone3D : Claim3D
{
    public IndustrialZoneType IndustrialType { get; set; }
    
    // Mining zones have specific depth ranges
    public MiningRights MiningRights { get; set; }
    
    // Environmental conditions that apply
    public List<EnvironmentalRestriction> Restrictions { get; set; }
    
    // For mining: registered extraction points
    public List<Vector3I> RegisteredMineShafts { get; set; }
}

public enum IndustrialZoneType
{
    SurfaceFactory,    // -5 to +20
    MiningOperation,   // Variable depth
    UndergroundFacility, // -100 to -30
    SkyPlatform        // +30 to +50
}
```

### 2.4 Public Infrastructure Claims

**3D corridors and utility networks**:

| Infrastructure | Dimensions | Y Range | Notes |
|----------------|------------|---------|-------|
| **Roads** | Width: 9-17 blocks | Surface to +2 | Includes sidewalks, lighting |
| **Highways** | Width: 25-33 blocks | Surface to +4 | Multi-block elevation |
| **Utilities** | 3×3 blocks | Variable | Sewers, power, data |
| **Rail/Transit** | 5×5 blocks | Variable tunnels/elevated | Grade-separated |
| **Air Corridors** | Variable | +40 to +56 | Designated flight paths |

```csharp
public class InfrastructureClaim3D : Claim3D
{
    public InfrastructureType InfrastructureType { get; set; }
    
    // Infrastructure often has linear extent
    public List<Vector3I> PathNodes { get; set; }
    public int PathWidth { get; set; }
    public int PathHeight { get; set; }
    
    // Elevation profile for non-flat infrastructure
    public ElevationProfile ElevationProfile { get; set; }
    
    // Easement rights (others can use but not obstruct)
    public bool IsEasement => true;
    public List<PermittedUse> PermittedUses { get; set; }
}
```

---

## 3. Subsurface Rights

### 3.1 Mining Rights Depth

**Stratified Mining Rights**:

| Depth Layer | Y Range | Resource Types | Rights Holder |
|-------------|---------|----------------|---------------|
| **Surface Mining** | -5 to -15 | Sand, gravel, clay | Surface owner |
| **Shallow Mining** | -15 to -40 | Coal, stone, common ores | Claim owner |
| **Deep Mining** | -40 to -100 | Iron, copper, gold | Licensed miners |
| **Bedrock Mining** | -100 to -200 | Gems, rare minerals | State/federal concession |

**Mining Rights Separation**:
```csharp
public class SubsurfaceRights
{
    public Claim3D ParentClaim { get; set; }
    
    // Can subsurface be separately claimed?
    public bool IsSeparable { get; set; }
    
    // Current subsurface owner (may differ from surface)
    public Entity SubsurfaceOwner { get; set; }
    
    // Depth ranges for different right types
    public Dictionary<MiningDepth, MiningRight> DepthRights { get; set; }
    
    // Registered mine shafts and adits
    public List<MineEntry3D> MineEntries { get; set; }
    
    // Extraction quotas and limits
    public ExtractionPermit ExtractionPermit { get; set; }
}

public enum MiningDepth
{
    Surface,      // -5 to -15
    Shallow,      // -15 to -40
    Deep,         // -40 to -100
    Bedrock       // -100 to -200
}
```

### 3.2 Underground Construction

**Subterranean Building Rights**:

| Structure Type | Min Depth | Max Depth | Requirements |
|----------------|-----------|-----------|--------------|
| **Basements** | -3 | -10 | Adjacent to surface building |
| **Underground Homes** | -10 | -30 | Ventilation, emergency exits |
| **Bunkers/Shelters** | -20 | -50 | Reinforced construction |
| **Mineshaft Infrastructure** | -10 | -200 | Hoists, ladders, ventilation |
| **Underground Cities** | -30 | -80 | Master planning, utilities |

```csharp
public class UndergroundConstructionPermit
{
    public ClaimBounds3D ConstructionZone { get; set; }
    public UndergroundStructureType StructureType { get; set; }
    
    // Structural requirements
    public int RequiredPillarSpacing { get; set; }  // For large excavations
    public bool RequiresReinforcement { get; set; }
    public int MinWallThickness { get; set; }
    
    // Safety requirements
    public int RequiredEmergencyExits { get; set; }
    public bool RequiresVentilationSystem { get; set; }
    public bool RequiresFloodProtection { get; set; }
    
    // Approval status
    public PermitStatus Status { get; set; }
    public List<InspectionCheckpoint> Inspections { get; set; }
}
```

### 3.3 Geological Resource Ownership

**Resource Rights by Strata** (aligned with terrain generation from Session 1):

| Strata | Y Range | Primary Resources | Ownership Model |
|--------|---------|-------------------|-----------------|
| **Topsoil** | 0 to -10 | Agriculture, sand | Surface owner |
| **Sedimentary** | -10 to -30 | Coal, clay, gravel | Surface owner |
| **Bedrock Contact** | -30 to -80 | Iron, copper, stone | Claim holder |
| **Granite Layer** | -80 to -120 | Granite, rare minerals | Concession system |
| **Gneiss/Obsidian** | -120 to -200 | Gems, obsidian | State-licensed |

**Resource Discovery Mechanic**:
```csharp
public class GeologicalSurveySystem
{
    // Prospecting determines what resources exist
    public ProspectingResult ProspectArea(ClaimBounds3D area)
    {
        // Use world generation data to determine resources
        var resources = new List<DiscoveredResource>();
        
        for (int y = area.MinY; y <= area.MaxY; y++)
        {
            var strata = GetStrataAtDepth(y);
            var oreVeins = FindOreVeinsInArea(area, y);
            
            resources.AddRange(oreVeins);
        }
        
        return new ProspectingResult
        {
            Area = area,
            DiscoveredResources = resources,
            SurveyQuality = CalculateSurveyQuality(area),
            EstimatedValue = resources.Sum(r => r.EstimatedValue)
        };
    }
}
```

### 3.4 Tunnel and Cave Rights

**Underground Passage Rights**:

| Passage Type | Width | Height | Ownership | Notes |
|--------------|-------|--------|-----------|-------|
| **Personal Tunnel** | 3 blocks | 3 blocks | Individual | Must stay under own claim |
| **Utility Tunnel** | 5 blocks | 4 blocks | Town/State | Easement rights |
| **Transport Tunnel** | 9 blocks | 6 blocks | Public | Transit corridors |
| **Natural Caves** | Variable | Variable | State | Cannot be claimed, can be used |

**Tunnel Intersection Rules**:
```csharp
public class TunnelRightsManager
{
    // Check if tunnel path is valid
    public TunnelPermitResult EvaluateTunnelProposal(
        Vector3I start, 
        Vector3I end, 
        int width, 
        int height,
        Entity proposer)
    {
        // Sample path through 3D space
        var path = Bresenham3D(start, end);
        
        foreach (var point in path)
        {
            // Check claims along path
            var claims = FindClaimsAt(point);
            
            foreach (var claim in claims)
            {
                // Must have permission or easement
                if (!HasTunnelPermission(claim, proposer))
                {
                    return TunnelPermitResult.Denied(
                        $"No tunnel rights through claim at {point}");
                }
            }
            
            // Check for existing tunnels
            var existingTunnels = FindTunnelsAt(point);
            if (existingTunnels.Any(t => t.Intersects(path, width, height)))
            {
                return TunnelPermitResult.Denied(
                    $"Would intersect existing tunnel at {point}");
            }
        }
        
        return TunnelPermitResult.Approved();
    }
}
```

---

## 4. Air Rights

### 4.1 Building Height Limits

**Height Zoning by District**:

| District Type | Max Height (Y) | Typical Use | Rationale |
|---------------|----------------|-------------|-----------|
| **Low-Rise Residential** | +12 | Homes, small buildings | Preserve neighborhood character |
| **Mid-Rise Mixed** | +20 | Apartments, offices | Moderate density |
| **High-Rise Commercial** | +30 | Towers, skyscrapers | Central business districts |
| **Sky Platforms** | +50 | Special structures | Requires special permit |
| **Air Corridor** | +40 to +56 | Transit, utilities | Public airspace |

**Height Limit Implementation**:
```csharp
public class AirRightsZoning
{
    public DistrictType District { get; set; }
    public int MaxBuildingHeight { get; set; }
    public int MaxStructureHeight { get; set; }  // Includes spires, antennas
    
    // Height can be traded/bought from neighbors
    public bool AllowsHeightTransfer { get; set; }
    public List<HeightTransfer> TransferredRights { get; set; }
    
    // Special height bonuses
    public int HeightBonusForPublicSpace { get; set; }
    public int HeightBonusForHistoricPreservation { get; set; }
    
    public bool IsHeightPermitted(int proposedHeight, Entity builder)
    {
        int effectiveLimit = MaxBuildingHeight;
        
        // Add transferred rights
        effectiveLimit += TransferredRights
            .Where(t => t.Recipient == builder)
            .Sum(t => t.HeightBonus);
            
        // Check bonuses
        if (builder.HasProvidedPublicSpace)
            effectiveLimit += HeightBonusForPublicSpace;
            
        return proposedHeight <= effectiveLimit;
    }
}
```

### 4.2 Overflight Permissions

**Airspace Access Rights**:

| Altitude | Y Range | Use | Permission Required |
|----------|---------|-----|---------------------|
| **Ground Effect** | 0 to +5 | Drones, hovercraft | Surface owner consent |
| **Low Altitude** | +5 to +20 | Personal vehicles | Town airspace permit |
| **Mid Altitude** | +20 to +40 | Commercial transit | Federal aviation license |
| **High Altitude** | +40 to +56 | Emergency/military | Federal clearance |

**Overflight Right Model**:
```csharp
public class OverflightRights
{
    // Low altitude requires surface owner consent
    public bool RequiresSurfaceConsent { get; set; }
    
    // Can surface owner deny overflight?
    public bool CanDenyOverflight { get; set; }
    
    // Compensation for overflight
    public float OverflightFeePerBlock { get; set; }
    
    // Public safety exceptions
    public bool EmergencyServicesExempt { get; set; } = true;
    public bool GovernmentEssentialExempt { get; set; } = true;
    
    // Check if overflight is permitted
    public OverflightPermission CheckPermission(
        Vector3I entryPoint, 
        Vector3I exitPoint,
        int altitude,
        Entity operator,
        FlightPurpose purpose)
    {
        // Public safety always permitted
        if (purpose == FlightPurpose.Emergency && EmergencyServicesExempt)
            return OverflightPermission.Granted();
            
        // Sample flight path
        var path = Interpolate3D(entryPoint, exitPoint);
        
        foreach (var point in path)
        {
            var surfaceClaim = FindSurfaceClaimAt(point);
            
            if (surfaceClaim != null && surfaceClaim.Owner != operator)
            {
                // Check if owner has denied overflight
                if (surfaceClaim.AirRights.CanDenyOverflight && 
                    surfaceClaim.AirRights.HasDeniedOverflightTo(operator))
                {
                    return OverflightPermission.Denied(
                        $"Overflight denied by owner of {surfaceClaim}");
                }
            }
        }
        
        return OverflightPermission.Granted();
    }
}
```

### 4.3 Sky Constructions

**Structures Above Ground Level**:

| Structure Type | Y Range | Support Method | Requirements |
|----------------|---------|----------------|--------------|
| **Bridges** | Variable | Towers/anchors | Span permits, load analysis |
| **Skyways** | +15 to +25 | Building connection | Fire safety, structural |
| **Aerial Platforms** | +30 to +50 | Pillars/cables | Special engineering permit |
| **Floating Islands** | +40 to +56 | Anti-gravity/magic | Legendary/federal only |
| **Space Elevator** | +56+ | Ground anchor | World wonder tier |

**Sky Construction Permits**:
```csharp
public class SkyConstructionPermit
{
    public ClaimBounds3D ConstructionBounds { get; set; }
    public SkyStructureType StructureType { get; set; }
    
    // Structural requirements
    public int MinSupportPillars { get; set; }
    public int MaxSpanWithoutSupport { get; set; }
    public bool RequiresWindAnalysis { get; set; }
    public bool RequiresLoadTesting { get; set; }
    
    // Impact assessments
    public ShadowImpactAssessment ShadowStudy { get; set; }
    public ViewImpactAssessment ViewStudy { get; set; }
    public SafetyAssessment SafetyStudy { get; set; }
    
    // Approved by
    public Entity ApprovingAuthority { get; set; }
    public DateTime ApprovalDate { get; set; }
    public DateTime ExpirationDate { get; set; }
}
```

### 4.4 View Preservation

**View Rights and Restrictions**:

| View Type | Protection Level | Enforceability | Notes |
|-----------|------------------|----------------|-------|
| **Existing Views** | Medium | Through zoning | Height restrictions |
| **Historic Views** | High | Legal protection | Landmark sightlines |
| **Natural Views** | Variable | Zoning/ covenant | Coastal, mountain views |
| **Solar Access** | Medium | Easement possible | Right to sunlight |

**View Preservation Implementation**:
```csharp
public class ViewPreservationEasement
{
    // The property being protected
    public Claim3D BenefitedProperty { get; set; }
    
    // Direction of protected view
    public Vector3 ViewDirection { get; set; }
    public float ViewAngle { get; set; }
    
    // Maximum obstruction allowed
    public float MaxObstructionHeight { get; set; }
    public int MaxObstructionDistance { get; set; }
    
    // Properties burdened by this easement
    public List<Claim3D> BurdenedProperties { get; set; }
    
    // Check if proposed construction violates view
    public bool ViolatesEasement(ClaimBounds3D proposedConstruction)
    {
        // Raycast from benefited property through view corridor
        var ray = new Ray3D(
            BenefitedProperty.Center,
            ViewDirection,
            MaxObstructionDistance);
            
        // Check if proposed construction intersects view corridor
        if (ray.Intersects(proposedConstruction))
        {
            // Check height of intersection
            float obstructionHeight = GetMaxHeightInCorridor(
                proposedConstruction, ray);
                
            return obstructionHeight > MaxObstructionHeight;
        }
        
        return false;
    }
}
```

---

## 5. Claim Creation UI

### 5.1 3D Visualization Tools

**Visual Claim Definition Interface**:

```csharp
public class Claim3DEditorUI
{
    // 3D viewport for claim visualization
    public SubViewport3D Viewport3D { get; set; }
    
    // Visual elements
    public ClaimVolumeVisualizer VolumeVisualizer { get; set; }
    public HeightPlaneVisualizer HeightPlane { get; set; }
    public DepthPlaneVisualizer DepthPlane { get; set; }
    public ClaimConflictHighlighter ConflictHighlighter { get; set; }
    
    // Interactive controls
    public void EnableClaimEditing(Claim3D existingClaim = null)
    {
        // Initialize visualizer
        VolumeVisualizer.Show();
        
        // Allow drag handles for bounds
        SetupDragHandles();
        
        // Show world context
        ShowSurroundingClaims();
        ShowTerrainProfile();
        ShowGeologicalLayers();
    }
    
    public void UpdateVisualization(ClaimBounds3D bounds)
    {
        // Update wireframe box
        VolumeVisualizer.UpdateBounds(bounds);
        
        // Color coding
        VolumeVisualizer.Color = IsValid(bounds) ? Colors.Green : Colors.Red;
        
        // Highlight conflicts
        var conflicts = FindConflicts(bounds);
        ConflictHighlighter.Highlight(conflicts);
        
        // Update cost display
        UpdateCostDisplay(bounds);
    }
}
```

**Visual Elements**:

| Element | Representation | Interaction |
|---------|----------------|-------------|
| **Claim Volume** | Translucent colored box | Drag to resize |
| **Surface Plane** | Grid at Y=0 | Reference level |
| **Height Plane** | Movable plane at MaxY | Slider adjusts |
| **Depth Plane** | Movable plane at MinY | Slider adjusts |
| **Conflicts** | Red hatching overlay | Shows overlapping claims |
| **Geology** | Side cross-section | Shows strata layers |

### 5.2 Height/Depth Sliders

**Vertical Extent Controls**:

```csharp
public class VerticalExtentControl
{
    public VSlider HeightSlider { get; set; }
    public VSlider DepthSlider { get; set; }
    public Label HeightLabel { get; set; }
    public Label DepthLabel { get; set; }
    
    public void Setup(int worldMinY, int worldMaxY)
    {
        // Height slider (positive Y from surface)
        HeightSlider.MinValue = 0;
        HeightSlider.MaxValue = worldMaxY;
        HeightSlider.ValueChanged += OnHeightChanged;
        
        // Depth slider (negative Y from surface)
        DepthSlider.MinValue = worldMinY;
        DepthSlider.MaxValue = 0;
        DepthSlider.ValueChanged += OnDepthChanged;
    }
    
    private void OnHeightChanged(float value)
    {
        int maxY = Mathf.RoundToInt(value);
        HeightLabel.Text = $"Height: {maxY}m (+{maxY} above surface)";
        
        // Update visualization
        Editor.UpdateHeight(maxY);
        
        // Update cost
        RecalculateCost();
    }
    
    private void OnDepthChanged(float value)
    {
        int minY = Mathf.RoundToInt(value);
        DepthLabel.Text = $"Depth: {Mathf.Abs(minY)}m ({minY} below surface)";
        
        // Update visualization
        Editor.UpdateDepth(minY);
        
        // Update cost
        RecalculateCost();
    }
}
```

### 5.3 Preview Render

**Real-time Claim Preview**:

```csharp
public class ClaimPreviewRenderer
{
    // Preview scene setup
    public Node3D PreviewContainer { get; set; }
    public Camera3D PreviewCamera { get; set; }
    
    public void RenderClaimPreview(ClaimBounds3D bounds)
    {
        // Clear previous preview
        ClearPreview();
        
        // Create wireframe box
        var wireframe = CreateWireframeBox(bounds);
        wireframe.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0, 1, 0, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        PreviewContainer.AddChild(wireframe);
        
        // Show cross-section at center
        var crossSection = GenerateCrossSection(bounds);
        PreviewContainer.AddChild(crossSection);
        
        // Highlight existing structures within bounds
        var existingStructures = FindStructuresInBounds(bounds);
        foreach (var structure in existingStructures)
        {
            var highlight = CreateHighlightMesh(structure);
            highlight.Material = new StandardMaterial3D
            {
                AlbedoColor = Colors.Yellow,
                EmissionEnabled = true,
                Emission = Colors.Yellow
            };
            PreviewContainer.AddChild(highlight);
        }
        
        // Position camera for optimal view
        PositionCamera(bounds);
    }
    
    private void PositionCamera(ClaimBounds3D bounds)
    {
        // Calculate isometric view angle
        var center = new Vector3(
            (bounds.MinX + bounds.MaxX) / 2f,
            (bounds.MinY + bounds.MaxY) / 2f,
            (bounds.MinZ + bounds.MaxZ) / 2f);
            
        var size = new Vector3(bounds.Width, bounds.Height, bounds.Depth);
        var distance = size.Length() * 1.5f;
        
        // Isometric angle: 45° horizontal, 30° vertical
        var direction = new Vector3(1, 0.5f, 1).Normalized();
        PreviewCamera.Position = center + direction * distance;
        PreviewCamera.LookAt(center);
    }
}
```

### 5.4 Cost Calculation (Volume-Based)

**Claim Cost Formula**:

```csharp
public class ClaimCostCalculator
{
    public struct CostFactors
    {
        public float BaseRatePerCubicMeter { get; set; }  // Credits/m³
        public float SurfacePremium { get; set; }          // Multiplier for surface area
        public float DepthDiscount { get; set; }           // Discount for deeper levels
        public float HeightPremium { get; set; }           // Premium for air rights
        public float ConflictPenalty { get; set; }         // Extra cost for conflicts
        public float InfrastructureCredit { get; set; }    // Discount for public benefit
    }
    
    public ClaimCost CalculateCost(ClaimBounds3D bounds, ClaimType type)
    {
        var factors = GetCostFactors(type);
        
        // Base volume cost
        float baseVolume = bounds.Volume;
        float baseCost = baseVolume * factors.BaseRatePerCubicMeter;
        
        // Surface premium (horizontal area at surface level)
        float surfaceArea = bounds.Width * bounds.Depth;
        float surfacePremium = surfaceArea * factors.SurfacePremium;
        
        // Depth adjustments (deeper = cheaper per m³ due to reduced utility)
        float avgDepth = Mathf.Abs((bounds.MinY + bounds.MaxY) / 2f);
        float depthDiscount = 1f - (avgDepth / 200f) * factors.DepthDiscount;
        
        // Height adjustments (air rights premium)
        float heightAboveSurface = Mathf.Max(0, bounds.MaxY);
        float heightPremium = heightAboveSurface * bounds.Width * bounds.Depth * factors.HeightPremium;
        
        // Total calculation
        float totalCost = (baseCost + surfacePremium + heightPremium) * depthDiscount;
        
        // Additional factors
        var conflicts = FindConflicts(bounds);
        if (conflicts.Any())
        {
            totalCost += conflicts.Count * factors.ConflictPenalty;
        }
        
        return new ClaimCost
        {
            BaseCost = baseCost,
            SurfacePremium = surfacePremium,
            DepthDiscount = depthDiscount,
            HeightPremium = heightPremium,
            TotalCost = Mathf.RoundToInt(totalCost),
            Currency = "Credits"
        };
    }
}
```

---

## 6. Law Enforcement in 3D

### 6.1 Spatial Law Conditions

**3D-Aware Law Triggers and Conditions**:

```csharp
public class SpatialLawConditions
{
    // Check if location is within claim bounds
    public static bool WithinClaim3D(Vector3I location, Claim3D claim)
    {
        return claim.Bounds.Contains(location.X, location.Y, location.Z);
    }
    
    // Check if within specific vertical zone
    public static bool WithinVerticalZone(Vector3I location, int minY, int maxY)
    {
        return location.Y >= minY && location.Y <= maxY;
    }
    
    // Check depth-based condition (mining laws)
    public static bool WithinMiningDepth(Vector3I location, MiningDepth depth)
    {
        var (min, max) = GetDepthRange(depth);
        return location.Y >= min && location.Y <= max;
    }
    
    // Check air rights zone
    public static bool WithinAirRights(Vector3I location)
    {
        return location.Y > 0 && location.Y <= 40;
    }
    
    // Distance-based 3D check
    public static bool WithinDistance3D(Vector3I point, Vector3I center, float radius)
    {
        return point.DistanceTo(center) <= radius;
    }
    
    // Line-of-sight check for view protection laws
    public static bool ObstructsView(
        Vector3I constructionPos, 
        Vector3I viewPoint, 
        Vector3I viewTarget)
    {
        // Check if constructionPos blocks line from viewPoint to viewTarget
        var viewRay = new Ray3D(viewPoint, viewTarget - viewPoint);
        return viewRay.IntersectsPoint(constructionPos);
    }
}
```

### 6.2 "Within Claim Boundary" Checks

**Boundary Evaluation System**:

```csharp
public class BoundaryEnforcementSystem
{
    public bool CanPerformAction(
        Vector3I location,
        ActionType action,
        Entity actor)
    {
        // Find jurisdiction at location
        var jurisdiction = _governance.GetJurisdictionAt(location.X, location.Z);
        
        if (jurisdiction == null)
        {
            // Unclaimed territory - apply default world rules
            return CanPerformActionInWilderness(location, action, actor);
        }
        
        // Check if location is within any 3D claim
        var claims = jurisdiction.GetClaimsIn3D(
            location.X, location.Y, location.Z);
            
        // If multiple claims overlap, use most specific/innermost
        var relevantClaim = claims
            .OrderBy(c => c.Bounds.Volume)
            .FirstOrDefault();
            
        if (relevantClaim != null)
        {
            // Check permissions on this specific claim
            return CheckClaimPermissions(relevantClaim, action, actor, location);
        }
        
        // Public land within jurisdiction - check public land laws
        return jurisdiction.HasPermission(actor, action, location);
    }
    
    private bool CanPerformActionInWilderness(
        Vector3I location, 
        ActionType action, 
        Entity actor)
    {
        // Bedrock layer always protected
        if (location.Y == -200)
            return false;
            
        // Some actions restricted in wilderness
        switch (action)
        {
            case ActionType.Mining when location.Y < -100:
                // Deep mining requires license anywhere
                return actor.HasLicense(LicenseType.DeepMining);
                
            case ActionType.Building when location.Y > 20:
                // Tall structures need permit anywhere
                return actor.HasPermit(PermitType.TallStructure);
                
            default:
                return true;
        }
    }
}
```

### 6.3 Height-Based Restrictions

**Vertical Zoning Enforcement**:

```csharp
public class HeightRestrictionEnforcer
{
    public EnforcementResult CheckHeightRestrictions(
        ClaimBounds3D proposedConstruction,
        Entity builder)
    {
        // Get zoning at this location
        var zoning = _zoning.GetZoningAt(
            proposedConstruction.CenterX, 
            proposedConstruction.CenterZ);
            
        // Check maximum height
        if (proposedConstruction.MaxY > zoning.MaxBuildingHeight)
        {
            return EnforcementResult.Violated(
                $"Construction exceeds maximum height of {zoning.MaxBuildingHeight}m",
                new HeightViolation
                {
                    ProposedHeight = proposedConstruction.MaxY,
                    MaxAllowed = zoning.MaxBuildingHeight,
                    Excess = proposedConstruction.MaxY - zoning.MaxBuildingHeight
                });
        }
        
        // Check minimum height (for air corridors)
        if (zoning.MinHeight > 0 && proposedConstruction.MinY < zoning.MinHeight)
        {
            return EnforcementResult.Violated(
                $"Construction below minimum height of {zoning.MinHeight}m",
                new HeightViolation
                {
                    IsMinimumViolation = true,
                    ProposedMin = proposedConstruction.MinY,
                    MinRequired = zoning.MinHeight
                });
        }
        
        // Check floor-area ratio (FAR) based on vertical profile
        var far = CalculateFAR(proposedConstruction);
        if (far > zoning.MaxFAR)
        {
            return EnforcementResult.Violated(
                $"Floor-area ratio of {far:F2} exceeds maximum of {zoning.MaxFAR:F2}");
        }
        
        return EnforcementResult.Compliant();
    }
    
    private float CalculateFAR(ClaimBounds3D bounds)
    {
        float footprint = bounds.Width * bounds.Depth;
        float totalVolume = bounds.Volume;
        
        // Simplified FAR: total volume / (footprint × reference height)
        return totalVolume / (footprint * 10f);
    }
}
```

### 6.4 Depth-Based Mining Laws

**Subsurface Regulation**:

```csharp
public class MiningLawEnforcer
{
    public EnforcementResult CheckMiningPermission(
        Vector3I mineLocation,
        MiningOperationType operationType,
        Entity miner)
    {
        // Determine depth classification
        var depth = ClassifyDepth(mineLocation.Y);
        
        // Check depth-specific licenses
        switch (depth)
        {
            case MiningDepth.Surface:
                // Surface mining generally permitted on owned land
                return CheckSurfaceMining(mineLocation, miner);
                
            case MiningDepth.Shallow:
                // Shallow mining requires basic mining license
                if (!miner.HasLicense(LicenseType.Mining))
                {
                    return EnforcementResult.Violated(
                        "Mining license required for operations below -15m");
                }
                return CheckSurfaceMining(mineLocation, miner);
                
            case MiningDepth.Deep:
                // Deep mining requires special permit
                if (!miner.HasPermit(PermitType.DeepMining))
                {
                    return EnforcementResult.Violated(
                        "Deep mining permit required for operations below -40m");
                }
                
                // Check for registered mine shaft
                if (!HasRegisteredShaft(mineLocation, miner))
                {
                    return EnforcementResult.Violated(
                        "Operations below -40m must use registered mine shafts");
                }
                return CheckSubsurfaceRights(mineLocation, miner);
                
            case MiningDepth.Bedrock:
                // Bedrock layer mining requires state concession
                if (!miner.HasLicense(LicenseType.StateMiningConcession))
                {
                    return EnforcementResult.Violated(
                        "State mining concession required for bedrock operations");
                }
                return CheckSubsurfaceRights(mineLocation, miner);
                
            default:
                return EnforcementResult.Violated("Invalid depth classification");
        }
    }
    
    private EnforcementResult CheckSubsurfaceRights(
        Vector3I location, 
        Entity miner)
    {
        // Check if subsurface rights are owned by someone else
        var surfaceClaim = FindSurfaceClaimAt(location);
        
        if (surfaceClaim != null && surfaceClaim.SubsurfaceRights?.SubsurfaceOwner != null)
        {
            var subsurfaceOwner = surfaceClaim.SubsurfaceRights.SubsurfaceOwner;
            
            if (subsurfaceOwner != miner)
            {
                // Check if miner has permission from subsurface owner
                if (!surfaceClaim.SubsurfaceRights.HasMiningPermission(miner))
                {
                    return EnforcementResult.Violated(
                        $"Subsurface rights owned by {subsurfaceOwner.Name}. " +
                        "Mining permission required.");
                }
            }
        }
        
        return EnforcementResult.Compliant();
    }
}
```

---

## 7. Conflict Resolution

### 7.1 Overlapping Claims

**3D Overlap Detection and Resolution**:

```csharp
public class OverlappingClaimResolver
{
    public enum OverlapResolutionStrategy
    {
        FirstComeFirstServed,    // Earlier claim wins
        SmallerWins,              // More specific claim wins
        VerticalSeparation,       // Split by height
        HorizontalSeparation,     // Split by area
        NegotiatedSettlement,     // Parties must agree
        ArbitrationRequired       // Third-party decides
    }
    
    public ClaimOverlapResult ResolveOverlap(Claim3D claim1, Claim3D claim2)
    {
        // Calculate overlap volume
        var overlap = CalculateOverlap(claim1.Bounds, claim2.Bounds);
        
        if (!overlap.HasValue)
        {
            return ClaimOverlapResult.NoOverlap();
        }
        
        // Check if one claim completely contains the other
        if (claim1.Bounds.Contains(claim2.Bounds))
        {
            // Claim2 is entirely within claim1
            return ResolveContainment(claim1, claim2);
        }
        
        if (claim2.Bounds.Contains(claim1.Bounds))
        {
            // Claim1 is entirely within claim2
            return ResolveContainment(claim2, claim1);
        }
        
        // Partial overlap - determine strategy
        var strategy = DetermineStrategy(claim1, claim2);
        
        switch (strategy)
        {
            case OverlapResolutionStrategy.FirstComeFirstServed:
                return ResolveByPriority(claim1, claim2, overlap.Value);
                
            case OverlapResolutionStrategy.SmallerWins:
                return ResolveBySpecificity(claim1, claim2, overlap.Value);
                
            case OverlapResolutionStrategy.VerticalSeparation:
                return ResolveByVerticalSplit(claim1, claim2, overlap.Value);
                
            case OverlapResolutionStrategy.HorizontalSeparation:
                return ResolveByHorizontalSplit(claim1, claim2, overlap.Value);
                
            case OverlapResolutionStrategy.NegotiatedSettlement:
                return ClaimOverlapResult.RequiresNegotiation(
                    claim1, claim2, overlap.Value);
                    
            case OverlapResolutionStrategy.ArbitrationRequired:
                return ClaimOverlapResult.RequiresArbitration(
                    claim1, claim2, overlap.Value);
                    
            default:
                return ClaimOverlapResult.Invalid();
        }
    }
    
    private ClaimOverlapResult ResolveBySpecificity(
        Claim3D claim1, 
        Claim3D claim2, 
        ClaimBounds3D overlap)
    {
        // Smaller claim (by volume) is considered more specific
        var smallerClaim = claim1.Bounds.Volume < claim2.Bounds.Volume ? claim1 : claim2;
        var largerClaim = claim1.Bounds.Volume < claim2.Bounds.Volume ? claim2 : claim1;
        
        // Smaller claim gets overlap zone
        return ClaimOverlapResult.Resolved(
            winningClaim: smallerClaim,
            losingClaim: largerClaim,
            resolution: new ClaimResolution
            {
                WinningBounds = smallerClaim.Bounds,
                LosingModifiedBounds = SubtractVolume(largerClaim.Bounds, overlap),
                OverlapZone = overlap
            });
    }
}
```

### 7.2 Vertical Disputes

**Height/Depth Conflict Resolution**:

```csharp
public class VerticalDisputeResolver
{
    public enum VerticalDisputeType
    {
        AirRightsOverlap,        // Competing air rights claims
        SurfaceSubsurfaceConflict, // Surface owner vs subsurface owner
        MiningBoundaryDispute,   // Conflicting mining operations
        TunnelIntersection,      // Tunnels crossing
        BuildingHeightViolation  // Structure too tall
    }
    
    public VerticalDisputeResolution ResolveVerticalDispute(
        Claim3D claim1,
        Claim3D claim2,
        VerticalDisputeType disputeType)
    {
        switch (disputeType)
        {
            case VerticalDisputeType.AirRightsOverlap:
                return ResolveAirRightsDispute(claim1, claim2);
                
            case VerticalDisputeType.SurfaceSubsurfaceConflict:
                return ResolveSurfaceSubsurfaceConflict(claim1, claim2);
                
            case VerticalDisputeType.MiningBoundaryDispute:
                return ResolveMiningBoundaryDispute(claim1, claim2);
                
            case VerticalDisputeType.TunnelIntersection:
                return ResolveTunnelIntersection(claim1, claim2);
                
            case VerticalDisputeType.BuildingHeightViolation:
                return ResolveHeightViolation(claim1, claim2);
                
            default:
                return VerticalDisputeResolution.Invalid();
        }
    }
    
    private VerticalDisputeResolution ResolveSurfaceSubsurfaceConflict(
        Claim3D surfaceClaim,
        Claim3D subsurfaceClaim)
    {
        // Check if subsurface rights were properly separated
        if (surfaceClaim.SubsurfaceRights?.IsSeparable == false)
        {
            // Subsurface not separable - surface owner wins
            return VerticalDisputeResolution.Resolved(
                victor: surfaceClaim,
                ruling: "Subsurface rights are not separable for this claim type",
                remedy: new Remedy
                {
                    Action: RemedyAction.RevokeClaim,
                    Target: subsurfaceClaim,
                    Compensation: CalculateCompensation(subsurfaceClaim)
                });
        }
        
        // Check dates - which was claimed first?
        if (surfaceClaim.CreatedAt < subsurfaceClaim.CreatedAt)
        {
            // Surface claimed first
            // Did surface owner explicitly reserve subsurface rights?
            if (surfaceClaim.SubsurfaceRights?.SubsurfaceOwner == null)
            {
                // Surface owner didn't reserve subsurface - subsurface claim valid
                return VerticalDisputeResolution.Resolved(
                    victor: subsurfaceClaim,
                    ruling: "Subsurface rights not reserved by surface owner",
                    remedy: new Remedy
                    {
                        Action = RemedyAction.ModifyBounds,
                        Target = surfaceClaim,
                        NewBounds = new ClaimBounds3D
                        {
                            MinX = surfaceClaim.Bounds.MinX,
                            MaxX = surfaceClaim.Bounds.MaxX,
                            MinZ = surfaceClaim.Bounds.MinZ,
                            MaxZ = surfaceClaim.Bounds.MaxZ,
                            MinY = 0,  // Surface only
                            MaxY = surfaceClaim.Bounds.MaxY
                        }
                    });
            }
        }
        
        // Complex case - require arbitration
        return VerticalDisputeResolution.RequiresArbitration(
            claim1: surfaceClaim,
            claim2: subsurfaceClaim,
            reason: "Both parties have valid claims to subsurface");
    }
}
```

### 7.3 Easements and Rights-of-Way

**3D Easement System**:

```csharp
public class Easement3D
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    // Dominant estate (benefited by easement)
    public Claim3D DominantEstate { get; set; }
    
    // Servient estate (burdened by easement)
    public Claim3D ServientEstate { get; set; }
    
    // 3D corridor of the easement
    public ClaimBounds3D EasementCorridor { get; set; }
    
    // Type of easement
    public EasementType Type { get; set; }
    
    // Permitted uses within corridor
    public List<PermittedUse> PermittedUses { get; set; }
    
    // Restrictions on servient estate
    public List<ServientRestriction> RestrictionsOnServient { get; set; }
    
    // Duration
    public EasementDuration Duration { get; set; }
    public DateTime? ExpirationDate { get; set; }
    
    // Transferability
    public bool IsTransferable { get; set; }
    public bool RunsWithLand { get; set; }  // Transfers with property sale
    
    public bool IsActive(Vector3I location, UseType use)
    {
        // Check if location is within easement corridor
        if (!EasementCorridor.Contains(location.X, location.Y, location.Z))
            return false;
            
        // Check if use is permitted
        if (!PermittedUses.Any(p => p.UseType == use))
            return false;
            
        // Check duration
        if (ExpirationDate.HasValue && DateTime.UtcNow > ExpirationDate.Value)
            return false;
            
        return true;
    }
}

public enum EasementType
{
    RightOfWay,        // Passage through property
    UtilityEasement,   // Pipes, wires, conduits
    Conservation,      // Restricted use for environmental protection
    Solar,             // Right to sunlight
    View,              // Right to unobstructed view
    Air,               // Air passage rights
    Support,           // Structural support for neighboring buildings
    UndergroundPassage // Tunnels, shafts
}
```

### 7.4 Arbitration System

**3D Claim Arbitration Process**:

```csharp
public class ClaimArbitrationSystem
{
    public ArbitrationCase InitiateArbitration(
        Claim3D claim1,
        Claim3D claim2,
        string disputeDescription)
    {
        var case_ = new ArbitrationCase
        {
            Id = Guid.NewGuid(),
            Claimant1 = claim1,
            Claimant2 = claim2,
            DisputeDescription = disputeDescription,
            FiledAt = DateTime.UtcNow,
            Status = ArbitrationStatus.Pending,
            
            // Automatically gather evidence
            Evidence = GatherEvidence(claim1, claim2),
            
            // Calculate overlap metrics
            OverlapMetrics = CalculateOverlapMetrics(claim1, claim2)
        };
        
        // Assign arbitrator(s)
        case_.Arbitrators = SelectArbitrators(case_);
        
        // Notify parties
        NotifyParties(case_);
        
        return case_;
    }
    
    public ArbitrationRuling RenderRuling(ArbitrationCase case_)
    {
        // Analyze claims
        var claim1Merits = AnalyzeClaimMerits(case_.Claimant1);
        var claim2Merits = AnalyzeClaimMerits(case_.Claimant2);
        
        // Consider 3D factors
        var volumetricAnalysis = AnalyzeVolumetricConflict(
            case_.Claimant1, 
            case_.Claimant2);
            
        // Determine most equitable split
        var proposedResolution = Propose3DResolution(
            case_.Claimant1,
            case_.Claimant2,
            volumetricAnalysis);
            
        // Generate ruling
        var ruling = new ArbitrationRuling
        {
            CaseId = case_.Id,
            RulingDate = DateTime.UtcNow,
            
            Resolution = proposedResolution,
            
            Rationale = BuildRationale(
                claim1Merits, 
                claim2Merits, 
                volumetricAnalysis),
                
            Remedies = DetermineRemedies(case_, proposedResolution),
            
            // Appeals
            AppealDeadline = DateTime.UtcNow.AddDays(7),
            AppealProcess = GetAppealProcess(case_)
        };
        
        // Execute ruling
        ExecuteRuling(ruling);
        
        return ruling;
    }
    
    private ClaimResolution Propose3DResolution(
        Claim3D claim1, 
        Claim3D claim2, 
        VolumetricAnalysis analysis)
    {
        // Strategy: minimize total volume loss
        
        if (analysis.CanSeparateVertically)
        {
            // Vertical separation is cleanest
            return new ClaimResolution
            {
                Type = ResolutionType.VerticalSplit,
                Claim1NewBounds = new ClaimBounds3D
                {
                    // Claim1 gets upper portion
                    MinX = claim1.Bounds.MinX,
                    MaxX = claim1.Bounds.MaxX,
                    MinZ = claim1.Bounds.MinZ,
                    MaxZ = claim1.Bounds.MaxZ,
                    MinY = analysis.OptimalSplitY,
                    MaxY = claim1.Bounds.MaxY
                },
                Claim2NewBounds = new ClaimBounds3D
                {
                    // Claim2 gets lower portion
                    MinX = claim2.Bounds.MinX,
                    MaxX = claim2.Bounds.MaxX,
                    MinZ = claim2.Bounds.MinZ,
                    MaxZ = claim2.Bounds.MaxZ,
                    MinY = claim2.Bounds.MinY,
                    MaxY = analysis.OptimalSplitY - 1
                }
            };
        }
        
        if (analysis.CanSeparateHorizontally)
        {
            // Horizontal separation
            return new ClaimResolution
            {
                Type = ResolutionType.HorizontalSplit,
                // ... similar structure for X/Z split
            };
        }
        
        // Cannot separate - one must be dissolved
        return new ClaimResolution
        {
            Type = ResolutionType.DissolveOne,
            DissolvedClaim = analysis.LessMeritoriousClaim,
            Compensation = analysis.RecommendedCompensation
        };
    }
}
```

---

## 8. Zoning in 3D

### 8.1 Residential Zones (Height Limits)

**3D Residential Zoning**:

```csharp
public class ResidentialZoning3D
{
    public ZoneType ZoneType { get; set; }  // Low, Medium, High density
    
    // Horizontal boundaries
    public Polygon2D ZoneBoundary { get; set; }
    
    // Vertical restrictions
    public int MinHeight { get; set; }  // Usually 0 or negative for basements
    public int MaxHeight { get; set; }
    public int HeightLimitReason { get; set; }
    
    // Setback requirements (horizontal)
    public int FrontSetback { get; set; }
    public int RearSetback { get; set; }
    public int SideSetback { get; set; }
    
    // Special 3D restrictions
    public bool RequiresUndergroundParking { get; set; }
    public int MinBasementDepth { get; set; }
    public bool RequiresStormShelter { get; set; }
    public int StormShelterDepth { get; set; }
    
    // Density calculations
    public float MaxFAR { get; set; }  // Floor area ratio
    public int MaxUnitsPerArea { get; set; }
    
    public bool IsCompliant(ClaimBounds3D construction, BuildingType type)
    {
        // Check height
        if (construction.MaxY > MaxHeight)
            return false;
            
        // Check FAR
        var far = CalculateFAR(construction);
        if (far > MaxFAR)
            return false;
            
        // Check setbacks
        if (!CheckSetbacks(construction))
            return false;
            
        // Check underground requirements
        if (RequiresUndergroundParking && !HasUndergroundParking(construction))
            return false;
            
        return true;
    }
}

public enum ZoneType
{
    RuralResidential,      // Very low density, large plots
    LowDensityResidential, // Single family, large yards
    MediumDensityResidential, // Townhomes, small lots
    HighDensityResidential,   // Apartments, condos
    MixedUseResidential       // Residential + small commercial
}
```

### 8.2 Industrial Zones (Ground Level)

**Industrial 3D Zoning**:

```csharp
public class IndustrialZoning3D
{
    public IndustrialZoneClass Class { get; set; }
    
    // Industrial zones typically focus on surface
    // But may extend underground for utilities
    
    public int SurfaceMinY { get; set; }  // Usually -5 to 0
    public int SurfaceMaxY { get; set; }  // Usually +10 to +20
    
    // Underground utility corridor
    public int UtilityMinY { get; set; }  // Usually -10 to -5
    public int UtilityMaxY { get; set; }  // Usually -5
    
    // Restrictions
    public bool RequiresEmissionControls { get; set; }
    public int MinBufferToResidential { get; set; }  // Meters
    public bool RequiresSoundBarriers { get; set; }
    
    // Hazardous material restrictions
    public bool AllowsHazardousMaterials { get; set; }
    public int HazardousStorageMinDepth { get; set; }  // Underground storage required
    
    public List<PermittedIndustrialUse> PermittedUses { get; set; }
}

public enum IndustrialZoneClass
{
    LightIndustrial,       // Assembly, small manufacturing
    GeneralIndustrial,     // Manufacturing, warehouses
    HeavyIndustrial,       // Heavy manufacturing, processing
    ExtractiveIndustrial,  // Mining, quarrying
    PortIndustrial         // Dockside, shipping
}
```

### 8.3 Mining Zones (Depth-Based)

**Subsurface Zoning for Resource Extraction**:

```csharp
public class MiningZoning3D
{
    // Mining zones are primarily depth-based
    
    public MiningZoneType ZoneType { get; set; }
    
    // Depth range for this zone
    public int MinDepth { get; set; }
    public int MaxDepth { get; set; }
    
    // Resources permitted to extract
    public List<ResourceType> PermittedResources { get; set; }
    
    // Extraction method restrictions
    public bool AllowsStripMining { get; set; }
    public bool AllowsShaftMining { get; set; }
    public bool AllowsTunnelMining { get; set; }
    
    // Environmental restrictions
    public bool RequiresReclamation { get; set; }
    public float ReclamationPercentage { get; set; }
    public bool RequiresGroundwaterProtection { get; set; }
    
    // Safety requirements
    public int MaxSlopeAngle { get; set; }  // For open pit
    public bool RequiresVentilationShaft { get; set; }
    public int MinVentilationShaftSpacing { get; set; }
    
    // Check if mining operation is permitted
    public bool IsMiningPermitted(
        Vector3I location,
        MiningMethod method,
        ResourceType resource)
    {
        // Check depth
        if (location.Y < MinDepth || location.Y > MaxDepth)
            return false;
            
        // Check resource
        if (!PermittedResources.Contains(resource))
            return false;
            
        // Check method
        switch (method)
        {
            case MiningMethod.Strip when !AllowsStripMining:
                return false;
            case MiningMethod.Shaft when !AllowsShaftMining:
                return false;
            case MiningMethod.Tunnel when !AllowsTunnelMining:
                return false;
        }
        
        return true;
    }
}

public enum MiningZoneType
{
    Prohibited,           // No mining allowed
    Restricted,           // Limited mining with permits
    Permitted,            // Standard mining allowed
    Encouraged,           // Tax incentives for mining
    Strategic             // State-controlled strategic resources
}
```

### 8.4 Mixed-Use Districts

**Vertical Mixed-Use Zoning**:

```csharp
public class MixedUseZoning3D
{
    public string DistrictName { get; set; }
    
    // Zoning by vertical layer
    public List<VerticalZoneLayer> VerticalLayers { get; set; }
    
    // Default structure
    public MixedUseZoning3D()
    {
        VerticalLayers = new List<VerticalZoneLayer>
        {
            // Surface/underground: parking, utilities
            new VerticalZoneLayer
            {
                MinY = -20,
                MaxY = -5,
                ZoneType = ZoneLayerType.Infrastructure,
                PermittedUses = { UseType.Parking, UseType.Utilities, UseType.Storage }
            },
            
            // Ground: retail, commercial
            new VerticalZoneLayer
            {
                MinY = -5,
                MaxY = +5,
                ZoneType = ZoneLayerType.Commercial,
                PermittedUses = { UseType.Retail, UseType.Restaurant, UseType.Office }
            },
            
            // Mid: offices, services
            new VerticalZoneLayer
            {
                MinY = +5,
                MaxY = +20,
                ZoneType = ZoneLayerType.Commercial,
                PermittedUses = { UseType.Office, UseType.Medical, UseType.Education }
            },
            
            // Upper: residential
            new VerticalZoneLayer
            {
                MinY = +20,
                MaxY = +40,
                ZoneType = ZoneLayerType.Residential,
                PermittedUses = { UseType.Residential, UseType.Hotel }
            },
            
            // Top: mechanical, possibly skybar
            new VerticalZoneLayer
            {
                MinY = +40,
                MaxY = +50,
                ZoneType = ZoneLayerType.Mechanical,
                PermittedUses = { UseType.Mechanical, UseType.Observation }
            }
        };
    }
    
    public bool IsUsePermitted(UseType use, int y)
    {
        var layer = VerticalLayers.FirstOrDefault(l => y >= l.MinY && y <= l.MaxY);
        if (layer == null)
            return false;
            
        return layer.PermittedUses.Contains(use);
    }
    
    public class VerticalZoneLayer
    {
        public int MinY { get; set; }
        public int MaxY { get; set; }
        public ZoneLayerType ZoneType { get; set; }
        public List<UseType> PermittedUses { get; set; }
        public float MaxFAR { get; set; }
        public int MaxHeight { get; set; }
    }
}
```

---

## 9. Technical Implementation

### 9.1 BoundingBox3D Structure

**Core 3D Bounding Box Implementation**:

```csharp
using System;
using System.Runtime.CompilerServices;
using Godot;

namespace Societies.Governance.Spatial
{
    /// <summary>
    /// Immutable 3D integer bounding box for claim representation
    /// </summary>
    public readonly struct BoundingBox3D : IEquatable<BoundingBox3D>
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MinZ;
        public readonly int MaxX;
        public readonly int MaxY;
        public readonly int MaxZ;
        
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public int Depth => MaxZ - MinZ + 1;
        public int Volume => Width * Height * Depth;
        public int SurfaceArea => Width * Depth;
        
        public Vector3I Min => new Vector3I(MinX, MinY, MinZ);
        public Vector3I Max => new Vector3I(MaxX, MaxY, MaxZ);
        public Vector3I Center => new Vector3I(
            (MinX + MaxX) / 2,
            (MinY + MaxY) / 2,
            (MinZ + MaxZ) / 2);
        
        public BoundingBox3D(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            if (minX > maxX || minY > maxY || minZ > maxZ)
                throw new ArgumentException("Min cannot exceed Max");
                
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }
        
        public BoundingBox3D(Vector3I min, Vector3I max)
            : this(min.X, min.Y, min.Z, max.X, max.Y, max.Z) { }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int x, int y, int z)
        {
            return x >= MinX && x <= MaxX &&
                   y >= MinY && y <= MaxY &&
                   z >= MinZ && z <= MaxZ;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Vector3I point) => Contains(point.X, point.Y, point.Z);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(BoundingBox3D other)
        {
            return MinX <= other.MinX && MaxX >= other.MaxX &&
                   MinY <= other.MinY && MaxY >= other.MaxY &&
                   MinZ <= other.MinZ && MaxZ >= other.MaxZ;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(BoundingBox3D other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX &&
                   MinY <= other.MaxY && MaxY >= other.MinY &&
                   MinZ <= other.MaxZ && MaxZ >= other.MinZ;
        }
        
        public BoundingBox3D? Intersection(BoundingBox3D other)
        {
            if (!Intersects(other))
                return null;
                
            return new BoundingBox3D(
                Math.Max(MinX, other.MinX),
                Math.Max(MinY, other.MinY),
                Math.Max(MinZ, other.MinZ),
                Math.Min(MaxX, other.MaxX),
                Math.Min(MaxY, other.MaxY),
                Math.Min(MaxZ, other.MaxZ));
        }
        
        public int IntersectionVolume(BoundingBox3D other)
        {
            var intersection = Intersection(other);
            return intersection?.Volume ?? 0;
        }
        
        public IEnumerable<Vector3I> GetAllPoints()
        {
            for (int x = MinX; x <= MaxX; x++)
                for (int y = MinY; y <= MaxY; y++)
                    for (int z = MinZ; z <= MaxZ; z++)
                        yield return new Vector3I(x, y, z);
        }
        
        public AABB ToAABB()
        {
            return new AABB(
                new Vector3(MinX, MinY, MinZ),
                new Vector3(Width, Height, Depth));
        }
        
        public bool Equals(BoundingBox3D other)
        {
            return MinX == other.MinX && MinY == other.MinY && MinZ == other.MinZ &&
                   MaxX == other.MaxX && MaxY == other.MaxY && MaxZ == other.MaxZ;
        }
        
        public override bool Equals(object obj) => obj is BoundingBox3D other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);
        public override string ToString() => $"[{MinX},{MinY},{MinZ}] - [{MaxX},{MaxY},{MaxZ}] ({Volume}m³)";
        
        public static bool operator ==(BoundingBox3D left, BoundingBox3D right) => left.Equals(right);
        public static bool operator !=(BoundingBox3D left, BoundingBox3D right) => !left.Equals(right);
    }
}
```

### 9.2 Spatial Query System

**3D Spatial Index for Claim Lookup**:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Societies.Governance.Spatial
{
    /// <summary>
    /// 3D spatial hash grid for efficient claim queries
    /// </summary>
    public class SpatialClaimIndex
    {
        private readonly Dictionary<Vector3I, HashSet<Claim3D>> _grid;
        private readonly int _cellSize;
        
        public SpatialClaimIndex(int cellSize = 16)  // Matches chunk size
        {
            _cellSize = cellSize;
            _grid = new Dictionary<Vector3I, HashSet<Claim3D>>();
        }
        
        // Convert world coordinates to grid cell
        private Vector3I WorldToCell(int x, int y, int z)
        {
            return new Vector3I(
                x / _cellSize,
                y / _cellSize,
                z / _cellSize);
        }
        
        public void IndexClaim(Claim3D claim)
        {
            // Get all cells this claim touches
            var minCell = WorldToCell(claim.Bounds.MinX, claim.Bounds.MinY, claim.Bounds.MinZ);
            var maxCell = WorldToCell(claim.Bounds.MaxX, claim.Bounds.MaxY, claim.Bounds.MaxZ);
            
            for (int cx = minCell.X; cx <= maxCell.X; cx++)
            {
                for (int cy = minCell.Y; cy <= maxCell.Y; cy++)
                {
                    for (int cz = minCell.Z; cz <= maxCell.Z; cz++)
                    {
                        var cell = new Vector3I(cx, cy, cz);
                        
                        if (!_grid.TryGetValue(cell, out var claims))
                        {
                            claims = new HashSet<Claim3D>();
                            _grid[cell] = claims;
                        }
                        
                        claims.Add(claim);
                    }
                }
            }
        }
        
        public void RemoveClaim(Claim3D claim)
        {
            foreach (var kvp in _grid)
            {
                kvp.Value.Remove(claim);
            }
        }
        
        public List<Claim3D> FindClaimsAt(int x, int y, int z)
        {
            var cell = WorldToCell(x, y, z);
            
            if (!_grid.TryGetValue(cell, out var claims))
                return new List<Claim3D>();
                
            // Filter to claims that actually contain point
            return claims.Where(c => c.Bounds.Contains(x, y, z)).ToList();
        }
        
        public List<Claim3D> FindClaimsInBox(BoundingBox3D box)
        {
            var result = new HashSet<Claim3D>();
            
            var minCell = WorldToCell(box.MinX, box.MinY, box.MinZ);
            var maxCell = WorldToCell(box.MaxX, box.MaxY, box.MaxZ);
            
            for (int cx = minCell.X; cx <= maxCell.X; cx++)
            {
                for (int cy = minCell.Y; cy <= maxCell.Y; cy++)
                {
                    for (int cz = minCell.Z; cz <= maxCell.Z; cz++)
                    {
                        var cell = new Vector3I(cx, cy, cz);
                        
                        if (_grid.TryGetValue(cell, out var claims))
                        {
                            foreach (var claim in claims)
                            {
                                if (claim.Bounds.Intersects(box))
                                    result.Add(claim);
                            }
                        }
                    }
                }
            }
            
            return result.ToList();
        }
        
        public List<Claim3D> FindOverlappingClaims(Claim3D claim)
        {
            return FindClaimsInBox(claim.Bounds)
                .Where(c => c.Id != claim.Id && c.Bounds.Intersects(claim.Bounds))
                .ToList();
        }
    }
}
```

### 9.3 Claim Validation

**Comprehensive Claim Validation System**:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Societies.Governance.Validation
{
    public class ClaimValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
        public List<ValidationWarning> Warnings { get; set; } = new List<ValidationWarning>();
    }
    
    public class ClaimValidator
    {
        private readonly SpatialClaimIndex _spatialIndex;
        private readonly World _world;
        private readonly IGovernanceSystem _governance;
        
        public ClaimValidator(SpatialClaimIndex index, World world, IGovernanceSystem governance)
        {
            _spatialIndex = index;
            _world = world;
            _governance = governance;
        }
        
        public ClaimValidationResult ValidateClaim(Claim3D claim, Entity claimant)
        {
            var result = new ClaimValidationResult();
            
            // 1. Validate bounds
            ValidateBounds(claim, result);
            
            // 2. Check for overlaps
            ValidateNoOverlaps(claim, result);
            
            // 3. Validate vertical extent
            ValidateVerticalExtent(claim, result);
            
            // 4. Check world boundaries
            ValidateWorldBounds(claim, result);
            
            // 5. Validate claim type requirements
            ValidateClaimType(claim, result);
            
            // 6. Check terrain constraints
            ValidateTerrain(claim, result);
            
            // 7. Check jurisdiction permissions
            ValidateJurisdiction(claim, claimant, result);
            
            // 8. Check financial requirements
            ValidateFinancial(claim, claimant, result);
            
            result.IsValid = !result.Errors.Any();
            return result;
        }
        
        private void ValidateBounds(Claim3D claim, ClaimValidationResult result)
        {
            var bounds = claim.Bounds;
            
            if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Depth <= 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "INVALID_DIMENSIONS",
                    Message = "Claim must have positive dimensions in all axes"
                });
            }
            
            if (bounds.Width > claim.Type.MaxWidth || 
                bounds.Depth > claim.Type.MaxDepth)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "EXCEEDS_MAX_HORIZONTAL",
                    Message = $"Claim exceeds maximum horizontal size for {claim.Type}"
                });
            }
            
            if (bounds.Volume > claim.Type.MaxVolume)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "EXCEEDS_MAX_VOLUME",
                    Message = $"Claim volume ({bounds.Volume}) exceeds maximum ({claim.Type.MaxVolume})"
                });
            }
        }
        
        private void ValidateNoOverlaps(Claim3D claim, ClaimValidationResult result)
        {
            var overlaps = _spatialIndex.FindOverlappingClaims(claim);
            
            if (overlaps.Any())
            {
                foreach (var overlap in overlaps)
                {
                    var intersection = claim.Bounds.Intersection(overlap.Bounds);
                    
                    if (intersection.HasValue)
                    {
                        result.Errors.Add(new ValidationError
                        {
                            Code = "OVERLAPPING_CLAIM",
                            Message = $"Claim overlaps with existing claim '{overlap.Name}'",
                            Details = new Dictionary<string, object>
                            {
                                ["OverlapVolume"] = intersection.Value.Volume,
                                ["OverlapBounds"] = intersection.Value,
                                ["ExistingClaimId"] = overlap.Id
                            }
                        });
                    }
                }
            }
        }
        
        private void ValidateVerticalExtent(Claim3D claim, ClaimValidationResult result)
        {
            // Bedrock layer is always protected
            if (claim.Bounds.MinY <= -200)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "BEDROCK_VIOLATION",
                    Message = "Claims cannot extend to bedrock layer (Y=-200)"
                });
            }
            
            // World ceiling
            if (claim.Bounds.MaxY > 56)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "EXCEEDS_WORLD_CEILING",
                    Message = "Claims cannot exceed world ceiling (Y=+56)"
                });
            }
            
            // Check depth-appropriate for claim type
            if (claim.Type == ClaimType.Personal && claim.Bounds.MinY < -100)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "DEEP_PERSONAL_CLAIM",
                    Message = "Personal claims below Y=-100 may require special mining permits"
                });
            }
        }
        
        private void ValidateWorldBounds(Claim3D claim, ClaimValidationResult result)
        {
            var worldRadius = _world.GetHorizontalRadius();
            
            if (Mathf.Abs(claim.Bounds.MinX) > worldRadius ||
                Mathf.Abs(claim.Bounds.MaxX) > worldRadius ||
                Mathf.Abs(claim.Bounds.MinZ) > worldRadius ||
                Mathf.Abs(claim.Bounds.MaxZ) > worldRadius)
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "OUTSIDE_WORLD_BOUNDS",
                    Message = "Claim extends outside world boundaries"
                });
            }
        }
        
        private void ValidateTerrain(Claim3D claim, ClaimValidationResult result)
        {
            // Check if claim intersects with important terrain features
            var samples = SampleTerrain(claim.Bounds);
            
            // Check for protected features
            if (samples.Any(s => s.IsProtectedFeature))
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "PROTECTED_TERRAIN",
                    Message = "Claim intersects with protected terrain features"
                });
            }
            
            // Warn about water coverage
            float waterCoverage = samples.Count(s => s.IsWater) / (float)samples.Count;
            if (waterCoverage > 0.5f)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "WATER_COVERAGE",
                    Message = $"Claim is {waterCoverage:P0} covered by water"
                });
            }
        }
        
        private List<TerrainSample> SampleTerrain(BoundingBox3D bounds)
        {
            var samples = new List<TerrainSample>();
            
            // Sample at regular intervals
            int sampleInterval = 8;
            
            for (int x = bounds.MinX; x <= bounds.MaxX; x += sampleInterval)
            {
                for (int z = bounds.MinZ; z <= bounds.MaxZ; z += sampleInterval)
                {
                    var surfaceY = _world.GetSurfaceHeight(x, z);
                    samples.Add(new TerrainSample
                    {
                        Position = new Vector3I(x, surfaceY, z),
                        IsWater = _world.IsWater(x, surfaceY, z),
                        IsProtectedFeature = _world.IsProtectedFeature(x, surfaceY, z)
                    });
                }
            }
            
            return samples;
        }
    }
}
```

### 9.4 Law Enforcement Checks

**3D-Aware Law Enforcement System**:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Societies.Governance.Enforcement
{
    public class SpatialLawEnforcer
    {
        private readonly SpatialClaimIndex _spatialIndex;
        private readonly IGovernanceSystem _governance;
        private readonly IEventBus _eventBus;
        
        public SpatialLawEnforcer(
            SpatialClaimIndex index, 
            IGovernanceSystem governance,
            IEventBus eventBus)
        {
            _spatialIndex = index;
            _governance = governance;
            _eventBus = eventBus;
        }
        
        /// <summary>
        /// Check if an action is permitted at a specific 3D location
        /// </summary>
        public EnforcementResult CanPerformAction(
            Vector3I location,
            ActionType action,
            Entity actor,
            Dictionary<string, object> context = null)
        {
            // 1. Find all applicable laws at this location
            var applicableLaws = GetApplicableLaws(location, action);
            
            // 2. Evaluate conditions for each law
            foreach (var law in applicableLaws.OrderByDescending(l => l.Priority))
            {
                var conditionContext = new ConditionContext
                {
                    Location = location,
                    Actor = actor,
                    Action = action,
                    AdditionalData = context ?? new Dictionary<string, object>()
                };
                
                // Check spatial conditions
                if (!EvaluateSpatialConditions(law, conditionContext))
                    continue;
                    
                // Check other conditions
                if (!law.EvaluateConditions(conditionContext))
                    continue;
                    
                // Law applies - determine outcome
                var outcome = law.DetermineOutcome(conditionContext);
                
                if (outcome.Type == OutcomeType.Prevent)
                {
                    return EnforcementResult.Prevented(law, outcome);
                }
                else if (outcome.Type == OutcomeType.Modify)
                {
                    return EnforcementResult.Modified(law, outcome);
                }
                else if (outcome.Type == OutcomeType.Tax)
                {
                    return EnforcementResult.Taxed(law, outcome);
                }
            }
            
            // No preventing laws found
            return EnforcementResult.Permitted();
        }
        
        private bool EvaluateSpatialConditions(Law law, ConditionContext context)
        {
            var location = context.Location;
            
            foreach (var condition in law.Conditions)
            {
                if (condition is WithinJurisdictionCondition wjc)
                {
                    var jurisdiction = _governance.GetJurisdictionAt(location.X, location.Z);
                    if (jurisdiction?.Id != wjc.JurisdictionId)
                        return false;
                }
                else if (condition is WithinClaimCondition wcc)
                {
                    var claims = _spatialIndex.FindClaimsAt(location.X, location.Y, location.Z);
                    if (!claims.Any(c => c.Id == wcc.ClaimId))
                        return false;
                }
                else if (condition is WithinHeightRangeCondition whrc)
                {
                    if (location.Y < whrc.MinY || location.Y > whrc.MaxY)
                        return false;
                }
                else if (condition is WithinDepthCondition wdc)
                {
                    var depth = -location.Y;  // Depth is negative Y
                    if (depth < wdc.MinDepth || depth > wdc.MaxDepth)
                        return false;
                }
                else if (condition is WithinDistance3DCondition w3dc)
                {
                    var distance = location.DistanceTo(w3dc.Center);
                    if (distance > w3dc.Radius)
                        return false;
                }
                else if (condition is AboveSurfaceCondition asc)
                {
                    var surfaceY = _world.GetSurfaceHeight(location.X, location.Z);
                    if (location.Y < surfaceY + asc.MinHeightAbove)
                        return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Get all laws that might apply to this action at this location
        /// </summary>
        private List<Law> GetApplicableLaws(Vector3I location, ActionType action)
        {
            var laws = new List<Law>();
            
            // Get jurisdiction
            var jurisdiction = _governance.GetJurisdictionAt(location.X, location.Z);
            if (jurisdiction != null)
            {
                laws.AddRange(jurisdiction.GetLawsForAction(action));
            }
            
            // Get claim-specific laws
            var claims = _spatialIndex.FindClaimsAt(location.X, location.Y, location.Z);
            foreach (var claim in claims)
            {
                laws.AddRange(claim.GetLocalLawsForAction(action));
            }
            
            // Get global laws
            laws.AddRange(_governance.GetGlobalLawsForAction(action));
            
            return laws.Distinct().ToList();
        }
        
        /// <summary>
        /// Validate a block modification request
        /// </summary>
        public BlockModificationResult ValidateBlockModification(
            Vector3I location,
            BlockData newBlock,
            Entity modifier)
        {
            // Check bedrock protection
            if (location.Y == -200)
            {
                return BlockModificationResult.Denied("Bedrock layer cannot be modified");
            }
            
            // Determine action type
            var action = DetermineActionType(location, newBlock);
            
            // Check laws
            var enforcement = CanPerformAction(location, action, modifier, new Dictionary<string, object>
            {
                ["NewBlock"] = newBlock,
                ["OldBlock"] = _world.GetBlock(location)
            });
            
            if (enforcement.IsPrevented)
            {
                return BlockModificationResult.Denied(
                    enforcement.PreventingLaw?.Description ?? "Action prevented by law");
            }
            
            // Check ownership/permissions
            var permission = CheckBlockPermission(location, modifier);
            if (!permission.IsPermitted)
            {
                return BlockModificationResult.Denied(permission.DenialReason);
            }
            
            return BlockModificationResult.Permitted(enforcement.Modifications);
        }
    }
}
```

---

## 10. Integration with Existing Systems

### 10.1 Integration with Voxel World System

**World-Claim Alignment**:

- Claim boundaries align with voxel grid (integer coordinates)
- Chunk-based indexing for performance (16×16×256)
- Bedrock layer (Y=-200) absolute exclusion zone
- Surface reference at Y=0 for consistent depth calculations
- Geological strata alignment with terrain generation layers

### 10.2 Integration with Governance System

**Existing Session 5 Integration**:

- Jurisdiction hierarchy applies to 3D claims (Town > State > Federal)
- Law system extended with 3D spatial conditions
- Voting mechanics for claim boundary changes
- Constitutional rights extended to 3D property
- Anti-griefing protections for vertical space

### 10.3 Integration with Economy

**Resource Extraction Rights**:

- Mining permits linked to subsurface rights
- Royalties on extracted resources
- Land value taxation based on 3D volume
- Air rights trading and transfers
- Easement compensation system

### 10.4 Integration with AI Systems

**AI Property Management**:

- AI agents can own and manage 3D claims
- Pathfinding respects claim boundaries
- AI evaluates 3D property value (depth, air rights, resources)
- Automated mining operations respect depth restrictions
- AI agents navigate tunnels and underground spaces

---

## 11. Performance Considerations

### 11.1 Query Performance Budgets

| Operation | Target Time | Max Time | Notes |
|-----------|-------------|----------|-------|
| Point lookup (claims at position) | <1ms | 5ms | Spatial hash lookup |
| Box query (claims in volume) | <5ms | 20ms | Range scan + filter |
| Overlap detection | <10ms | 50ms | Pairwise intersection |
| Law evaluation | <1ms | 5ms | Per law, cached |
| Full validation | <100ms | 500ms | Complete claim check |

### 11.2 Memory Budgets

| Data Structure | Per Item | Scaling | Notes |
|----------------|----------|---------|-------|
| Claim3D object | ~512 bytes | Linear | Bounds + metadata |
| Spatial index entry | ~64 bytes | Linear | Per cell reference |
| Active laws cache | ~1KB | Small | Recently evaluated |
| Validation buffer | ~4KB | Per operation | Temporary working set |

### 11.3 Optimization Strategies

1. **Spatial Hashing**: O(1) average lookup by grid cell
2. **Lazy Evaluation**: Only validate on modification
3. **Caching**: Cache law evaluation results per location
4. **Level-of-Detail**: Simplified bounds for distant claims
5. **Batch Processing**: Group validation queries
6. **Background Loading**: Async claim data loading

---

## 12. Success Criteria

### 12.1 Must Achieve

- [ ] 3D claim creation with X/Y/Z bounds
- [ ] Collision detection between 3D claims
- [ ] Vertical separation of surface/subsurface/air rights
- [ ] Spatial query system (<5ms per query)
- [ ] 3D visualization UI for claim definition
- [ ] Volume-based cost calculation
- [ ] Height/depth-based law enforcement
- [ ] Overlap resolution system

### 12.2 Should Achieve

- [ ] Subsurface rights trading
- [ ] Air rights transfer market
- [ ] 3D easement creation and enforcement
- [ ] Arbitration system for conflicts
- [ ] Geological resource rights management
- [ ] Tunnel/t intersection management
- [ ] View preservation easements
- [ ] Mixed-use vertical zoning

### 12.3 Nice to Have

- [ ] 3D claim templates (mineshaft, skyscraper)
- [ ] Automated boundary dispute resolution
- [ ] AI-driven property valuation
- [ ] Historical claim boundary evolution
- [ ] 3D property market analytics
- [ ] Dynamic zoning adjustment
- [ ] Underground city master planning tools

---

## 13. Research and References

### 13.1 Real-World Property Law References

| Concept | Real-World Example | Game Application |
|---------|-------------------|------------------|
| Mineral Rights | Texas mineral estates | Subsurface mining rights |
| Air Rights | NYC Transferable Development Rights | Building height trading |
| Strata Title | Singapore condominium law | Mixed-use vertical ownership |
| Easements | Utility right-of-way | Infrastructure corridors |
| Ad Coelum | Ancient common law | Default ownership extent |
| View Rights | California solar easements | Skyline protection |
| Zoning | Euclidean zoning (US) | 3D land use regulation |

### 13.2 Technical References

- Session 1 Voxel World System: Coordinate system, chunk architecture
- Session 1 Terrain Generation: Geological strata, resource distribution
- Session 5 Governance: Law system, jurisdiction, voting
- Session 2 AI: Agent behavior, property valuation

### 13.3 Game References

| Game | System | Relevance |
|------|--------|-----------|
| Minecraft | Claim plugins (GriefPrevention) | 2D claim basics |
| EVE Online | POS/Structure ownership | Volumetric space control |
| Eco | Property system | Resource rights |
| Wurm Online | Deed system | Depth-based mining |
| Landmark | Claims | 3D building zones |

---

**Navigation**: [Session 5 Index]([AGENTS-READ-FIRST]-index.md) | [Main Governance Document](./day5-governance-mechanics.md) | [Session 1 Voxel World](../session-1-technical-architecture/13-voxel-world-system.md)

**Status**: Draft - Ready for Review

**Document Version**: 1.0
