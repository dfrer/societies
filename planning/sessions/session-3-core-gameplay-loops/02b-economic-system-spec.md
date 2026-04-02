# Economic System Specification

**Session**: 3 - Core Gameplay Loops  
**Document**: 02b-economic-system-spec.md  
**Status**: Draft  
**Last Updated**: 2026-02-01  
**Dependencies**: technical-constants.md, Session 2 - 02-economic-behavior.md

---

## Overview

This document defines the complete economic system for Societies, including currency mechanics, pricing models, trading systems, contracts, banking, taxation, and economic indicators. All numerical values are cross-referenced with `planning/meta/technical-constants.md` for consistency.

---

## 1. Currency System

### 1.1 Currency Unit: Credit (¢)

```
Symbol: ¢ or "cr"
Format: Integer (no decimals)
Physical: Coins (1, 5, 10, 50, 100, 500, 1000 denominations)
Digital: Bank account accessible at town halls
Maximum Balance: 1,000,000¢ (soft cap, see technical-constants.md)
```

**Physical Currency**:
- Coins are physical items that can be dropped, stored, or stolen
- Stack size: 100 coins per inventory slot
- Weight: 0.01 kg per coin
- Digital conversion: 1% fee at banks

**Digital Currency**:
- Stored in bank accounts
- Accessible via town hall terminals
- Transferable to other accounts (instant, no fee within same jurisdiction)
- Cross-jurisdiction transfers: 2% fee

### 1.2 Money Supply Mechanics

#### Initial Distribution (per technical-constants.md)

```
New Player Starting Credits: 100¢
New AI Agent Starting Credits: 100¢
Minimum Survival Threshold: 50¢ (below this triggers welfare if enabled)
```

**Context**: Starting credits are calibrated to allow:
- 2-3 days of food purchase (20-30¢)
- Basic tool acquisition (50-100¢)
- Emergency funds for unexpected needs (20-30¢)

#### Faucets (Money Creation)

| Source | Amount Range | Frequency | Conditions |
|--------|-------------|-----------|------------|
| Selling to AI agents | Variable (market-based) | Per transaction | Market demand dependent |
| Contract completion | 10-500¢ | Per contract | Based on contract value |
| Government subsidies | Variable | Law-dependent | Must be enacted by governance |
| Resource extraction | Raw material value | Per unit | Market price for raw goods |
| Welfare programs | 20-50¢/day | Daily | If enabled by law, income < 50¢ |
| Starting credits | 100¢ | One-time | New player/agent only |

**Faucet Balancing**:
- Total faucet rate should maintain 2-5% annual inflation
- Primary faucet: AI agent purchasing (80% of new money)
- Secondary faucet: Contracts (15% of new money)
- Tertiary faucet: Government programs (5% of new money)

#### Sinks (Money Destruction)

| Sink | Cost Range | Frequency | Purpose |
|------|-----------|-----------|---------|
| Tool purchase | 20-400¢ | Per tool | Equipment acquisition |
| Land claims | 100-5,000¢ | One-time | Territory control |
| Taxes | 1-10% of transactions | Per transaction | Government revenue |
| Building materials | 5-50¢ per unit | Per construction | Infrastructure costs |
| Food purchase | 2-15¢ per unit | Daily consumption | Survival needs |
| Repair costs | Tool-dependent | When damaged | Maintenance |
| Fines/Penalties | 10-500¢ | Law violations | Deterrence |
| Travel fees | 5-50¢ | Per border crossing | Inter-jurisdiction movement |
| Store listing fees | 1% of item value/day | Daily | Market maintenance |
| Bank loan interest | 2%/week | Weekly | Credit cost |

**Sink Balancing**:
- Sinks must roughly equal faucets over time
- Primary sink: Taxes (40% of money destruction)
- Secondary sink: Land claims and building (30%)
- Tertiary sink: Consumables and repairs (20%)
- Quaternary sink: Penalties and fees (10%)

#### Inflation Targeting

```
Target Inflation: 2-5% per game year (28 real hours)
Measurement: Price index basket of 20 common goods
Adjustment Mechanism:
  - If inflation > 5%: Increase tax rates, reduce subsidies
  - If inflation < 2%: Increase subsidies, reduce tax rates
  - If deflation: Emergency stimulus via welfare programs
```

**Game Year**: 28 days (4 seasons × 7 days) = 28 real hours  
**Calculation**: Total credits in circulation ÷ (GDP × Velocity) = Target ratio

---

## 2. Price Reference Tables

### 2.1 Day 1 Economy (Early Game - Survival Phase)

New world, limited resources, high scarcity, inflated prices.

| Item | Base Price | Range | Typical | Stack Size | Notes |
|------|-----------|-------|---------|------------|-------|
| **Raw Materials** |
| Wood (1 unit) | 5¢ | 3-8¢ | 5¢ | 100 | Abundant but high demand |
| Stone (1 unit) | 3¢ | 2-5¢ | 3¢ | 50 | Essential for tools |
| Iron Ore | 8¢ | 5-12¢ | 8¢ | 50 | Requires mining skill |
| Iron Ingot | 15¢ | 10-20¢ | 15¢ | 50 | Smelted ore |
| Copper Ore | 6¢ | 4-9¢ | 6¢ | 50 | Alternative metal |
| **Food** |
| Berries (raw) | 4¢ | 2-6¢ | 4¢ | 20 | Foraged, low nutrition |
| Food (cooked) | 10¢ | 6-15¢ | 10¢ | 20 | Prepared meals |
| Meat (raw) | 6¢ | 4-10¢ | 6¢ | 20 | Hunting required |
| Meat (cooked) | 12¢ | 8-18¢ | 12¢ | 20 | Better nutrition |
| **Tools** |
| Stone Tool | 30¢ | 20-40¢ | 30¢ | 1 | Basic durability (50 uses) |
| Stone Pickaxe | 35¢ | 25-45¢ | 35¢ | 1 | Mining specialized |
| Iron Tool | 100¢ | 80-120¢ | 100¢ | 1 | 3× durability, 1.5× efficiency |
| Iron Pickaxe | 120¢ | 100-140¢ | 120¢ | 1 | Mining specialized |
| **Structures** |
| Simple Shelter | 200¢ | 150-300¢ | 200¢ | N/A | 2×2m basic structure |
| Campfire | 50¢ | 40-60¢ | 50¢ | N/A | Cooking, light, warmth |
| Storage Chest | 80¢ | 60-100¢ | 80¢ | N/A | 32 slot storage |
| **Claims** |
| Land Claim (basic) | 100¢ | 80-120¢ | 100¢ | N/A | 100m × 100m area |
| Land Claim (premium) | 500¢ | 400-600¢ | 500¢ | N/A | Better location |

**Day 1 Economic Context**:
- 25-100 agents + 1-8 players competing for resources
- No established production chains
- High tool demand for resource gathering
- Food scarcity drives high prices

### 2.2 Day 7 Economy (Established Phase)

Production chains established, competition increases, prices stabilize downward.

| Item | Base Price | Range | Typical | Day 1 Ratio | Notes |
|------|-----------|-------|---------|-------------|-------|
| **Raw Materials** |
| Wood | 4¢ | 3-6¢ | 4¢ | -20% | Supply chains established |
| Stone | 2¢ | 1.5-3¢ | 2¢ | -33% | Abundant supply |
| Iron Ore | 6¢ | 4-8¢ | 6¢ | -25% | Mining operations active |
| Iron Ingot | 12¢ | 8-16¢ | 12¢ | -20% | Smelting efficiency |
| Steel Ingot | 40¢ | 30-50¢ | 40¢ | NEW | Advanced processing |
| Copper Ingot | 10¢ | 7-14¢ | 10¢ | NEW | Secondary metal |
| **Food** |
| Food (cooked) | 8¢ | 5-12¢ | 8¢ | -20% | Agricultural production |
| Bread | 6¢ | 4-9¢ | 6¢ | NEW | Farming established |
| Stew | 15¢ | 10-20¢ | 15¢ | NEW | Complex cooking |
| **Tools** |
| Iron Tool | 80¢ | 60-100¢ | 80¢ | -20% | Mass production |
| Steel Tool | 250¢ | 200-300¢ | 250¢ | NEW | Premium tier |
| Copper Tool | 60¢ | 45-75¢ | 60¢ | NEW | Budget option |
| **Structures** |
| Workshop | 1,500¢ | 1,200-2,000¢ | 1,500¢ | NEW | Crafting station |
| Smithy | 2,000¢ | 1,600-2,400¢ | 2,000¢ | NEW | Metal working |
| Farm Plot | 300¢ | 250-400¢ | 300¢ | NEW | Agricultural |
| Advanced Building | 5,000¢ | 4,000-6,000¢ | 5,000¢ | NEW | Multi-room structure |
| **Services** |
| Labor (1 hour) | 50¢ | 30-80¢ | 50¢ | NEW | Skilled/unskilled |
| Crafting service | 100¢ | 70-130¢ | 100¢ | NEW | Specialized skill |
| Repair service | 40¢ | 30-50¢ | 40¢ | NEW | Tool maintenance |

**Day 7 Economic Context**:
- Specialization emerges (farmers, miners, crafters)
- Tool durability mechanics create replacement demand
- Steel tier unlocks for advanced players
- Contract labor market becomes viable

### 2.3 Day 30 Economy (Mature Phase)

Full economic complexity, luxury goods, political economy, price efficiency.

| Category | Day 1 Price | Day 30 Price | Ratio | Notes |
|----------|-------------|--------------|-------|-------|
| **Basic Resources** |
| Wood | 5¢ | 2.5¢ | -50% | Mass production, automation |
| Stone | 3¢ | 1.5¢ | -50% | Mining infrastructure |
| Iron Ore | 8¢ | 4¢ | -50% | Efficient extraction |
| **Processed Goods** |
| Iron Ingot | 15¢ | 10.5¢ | -30% | Efficient smelting |
| Steel Ingot | N/A | 28¢ | -30% from Day 7 | Supply chain mature |
| Food (cooked) | 10¢ | 7¢ | -30% | Agricultural surplus |
| **Advanced Technology** |
| Automation Device | N/A | 1,000-5,000¢ | NEW | Reduces labor needs |
| Advanced Tool | N/A | 500-1,500¢ | NEW | Masterwork tier |
| Rare Materials | N/A | 100-500¢ | NEW | Limited spawn resources |
| **Luxury Goods** |
| Decorated Items | N/A | 500-2,000¢ | NEW | Status symbols |
| Rare Collectibles | N/A | 200-1,000¢ | NEW | Limited availability |
| Custom Structures | N/A | 2,000-10,000¢ | NEW | Unique architecture |
| **Professional Services** |
| Skilled Labor | N/A | 100-200¢/hr | 2-4× Day 7 | Expert-level work |
| Consulting | N/A | 300-800¢/hr | NEW | Strategic advice |
| Political Service | N/A | 1,000-10,000¢ | NEW | Governance roles |

**Day 30 Economic Context**:
- GDP measured in 10,000s of credits
- Wealth inequality emerges (Gini coefficient tracked)
- Political economy becomes significant
- Luxury market separate from survival economy
- Speculation and investment possible

---

## 3. Dynamic Pricing Formula

### 3.1 Core Price Calculation Algorithm

```csharp
public class DynamicPricingEngine
{
    // Configuration constants
    private const float SUPPLY_FACTOR_MAX = 0.7f;      // Abundant supply discount
    private const float SUPPLY_FACTOR_MIN = 1.0f;      // Scarce supply premium
    private const float DEMAND_FACTOR_MIN = 1.0f;      // Normal demand baseline
    private const float DEMAND_FACTOR_MAX = 1.5f;      // High demand premium
    private const float SMOOTHING_FACTOR = 0.1f;       // 10% change per update
    private const float PRICE_UPDATE_INTERVAL_TICKS = 300; // Every 15 seconds
    
    public float CalculateMarketPrice(ItemType item, Market market, int currentTick)
    {
        // Only update periodically to prevent volatility
        if (currentTick % PRICE_UPDATE_INTERVAL_TICKS != 0)
            return market.GetCurrentPrice(item);
        
        float basePrice = GetBasePrice(item);
        
        // Supply factor: More stock = lower price (30% max discount)
        float currentStock = market.GetStock(item);
        float maxCapacity = market.GetMaxCapacity(item);
        float supplyRatio = currentStock / maxCapacity;
        float supplyFactor = 1.0f - ((supplyRatio - 0.5f) * 0.6f);
        supplyFactor = Mathf.Clamp(supplyFactor, SUPPLY_FACTOR_MAX, SUPPLY_FACTOR_MIN);
        
        // Demand factor: More buy orders = higher price (50% max premium)
        float pendingOrders = market.GetPendingBuyOrders(item);
        float typicalVelocity = GetTypicalVelocity(item);
        float demandRatio = pendingOrders / typicalVelocity;
        float demandFactor = 1.0f + ((demandRatio - 1.0f) * 0.5f);
        demandFactor = Mathf.Clamp(demandFactor, DEMAND_FACTOR_MIN, DEMAND_FACTOR_MAX);
        
        // Quality modifier: Higher quality = higher price
        float qualityModifier = 1.0f + (item.QualityLevel * 0.1f);
        // Quality levels: 0=Poor(1.0), 1=Normal(1.1), 2=Good(1.2), 3=Excellent(1.3), 4=Masterwork(1.5)
        
        // Location factor based on market position
        float locationFactor = GetLocationPriceModifier(market.Location);
        // Remote: +20%, Town center: 1.0, Production hub: -10%
        
        // Calculate raw price
        float calculatedPrice = basePrice * 
                               supplyFactor * 
                               demandFactor * 
                               qualityModifier * 
                               locationFactor;
        
        // Smooth price changes to prevent wild swings
        float currentPrice = market.GetCurrentPrice(item);
        float smoothedPrice = Mathf.Lerp(currentPrice, calculatedPrice, SMOOTHING_FACTOR);
        
        // Round to integer credits
        return Mathf.Round(smoothedPrice);
    }
    
    private float GetLocationPriceModifier(Location location)
    {
        switch (location.Type)
        {
            case LocationType.Remote:
                return 1.2f;  // Transportation costs
            case LocationType.TownCenter:
                return 1.0f;  // Baseline
            case LocationType.ProductionHub:
                return 0.9f;  // Local abundance
            case LocationType.Border:
                return 1.1f;  // Import/export dynamics
            default:
                return 1.0f;
        }
    }
    
    private float GetTypicalVelocity(ItemType item)
    {
        // Historical average transactions per hour for this item
        return MarketHistory.GetAverageVelocity(item, hours: 24);
    }
}
```

### 3.2 AI Price Belief System

AI agents maintain subjective price beliefs based on their observations and experiences.

```csharp
public class PriceBelief
{
    public ItemType Item { get; set; }
    public float MeanPrice { get; set; }
    public float Uncertainty { get; set; }  // ± percentage (0.1 = 10%)
    public float MinObserved { get; set; }
    public float MaxObserved { get; set; }
    public int ObservationCount { get; set; }
    public int LastUpdateTick { get; set; }
    
    // Personality bias
    private readonly AgentPersonality _personality;
    
    public PriceBelief(ItemType item, AgentPersonality personality)
    {
        Item = item;
        _personality = personality;
        
        // Initialize with wide uncertainty
        MeanPrice = GetDefaultPrice(item);
        Uncertainty = 3.0f;  // 300% uncertainty initially
        MinObserved = MeanPrice * 0.1f;
        MaxObserved = MeanPrice * 10.0f;
        ObservationCount = 0;
    }
    
    public void Update(float observedPrice, int currentTick)
    {
        // Bayesian-style weighted update
        float weight = 1.0f / (ObservationCount + 1);
        MeanPrice = (MeanPrice * (1 - weight)) + (observedPrice * weight);
        
        // Uncertainty decreases with more observations
        // Formula: 50% / sqrt(observations + 1)
        // Range: 50% (1 observation) → 5% (100+ observations)
        Uncertainty = 0.5f / Mathf.Sqrt(ObservationCount + 1);
        Uncertainty = Mathf.Clamp(Uncertainty, 0.05f, 3.0f);
        
        // Update observed range
        MinObserved = Mathf.Min(MinObserved, observedPrice);
        MaxObserved = Mathf.Max(MaxObserved, observedPrice);
        
        ObservationCount++;
        LastUpdateTick = currentTick;
        
        // Apply personality bias based on greed trait
        float greedBias = _personality.Greed / 100f * 0.2f;  // ±20% max bias
        if (_personality.Greed > 50)
        {
            // Greedy agents think prices should be higher
            MeanPrice *= (1 + greedBias);
        }
        else
        {
            // Thrifty agents expect lower prices
            MeanPrice *= (1 - greedBias);
        }
    }
    
    public bool IsGoodDeal(float price, TradeType type)
    {
        float threshold = 0.1f;  // 10% margin
        
        if (type == TradeType.Buy)
        {
            // For buying, price should be below our mean minus uncertainty buffer
            float maxWilling = MeanPrice * (1 - threshold);
            return price <= maxWilling;
        }
        else
        {
            // For selling, price should be above our mean plus uncertainty buffer
            float minWilling = MeanPrice * (1 + threshold);
            return price >= minWilling;
        }
    }
    
    public float GetWillingnessToTrade(float currentPrice, TradeType type)
    {
        // Returns 0-1 score of how willing agent is to trade at this price
        if (type == TradeType.Buy)
        {
            if (currentPrice > MeanPrice * 1.2f) return 0.0f;  // Too expensive
            if (currentPrice < MeanPrice * 0.8f) return 1.0f;  // Great deal
            return 1.0f - ((currentPrice - MeanPrice * 0.8f) / (MeanPrice * 0.4f));
        }
        else
        {
            if (currentPrice < MeanPrice * 0.8f) return 0.0f;  // Too cheap
            if (currentPrice > MeanPrice * 1.2f) return 1.0f;  // Great deal
            return (currentPrice - MeanPrice * 0.8f) / (MeanPrice * 0.4f);
        }
    }
}
```

### 3.3 Market Price Propagation

Price information spreads through the agent population via observation and communication.

```csharp
public class PricePropagationSystem
{
    // How agents learn about prices
    
    public void OnTransactionCompleted(Transaction transaction, int currentTick)
    {
        // Direct observers update immediately
        var nearbyAgents = GetAgentsInRadius(transaction.Location, 50f);
        foreach (var agent in nearbyAgents)
        {
            agent.PriceBeliefs[transaction.Item].Update(transaction.Price, currentTick);
        }
        
        // Record for gossip propagation
        MarketGossip.RecordTransaction(transaction, currentTick);
    }
    
    public void PropagateViaGossip(int currentTick)
    {
        // Agents gossip about prices during social interactions
        foreach (var conversation in ActiveConversations)
        {
            if (Random.value < 0.3f)  // 30% chance to discuss prices
            {
                var item = SelectItemToDiscuss(conversation);
                var belief = conversation.Initiator.PriceBeliefs[item];
                
                // Share price knowledge
                conversation.Recipient.PriceBeliefs[item].Merge(belief);
            }
        }
    }
    
    public void UpdateFromStoreVisits(int currentTick)
    {
        // Agents update beliefs when browsing stores
        foreach (var agent in ActiveAgents)
        {
            if (agent.CurrentActivity == Activity.Shopping)
            {
                var store = agent.TargetStore;
                foreach (var listing in store.Listings)
                {
                    agent.PriceBeliefs[listing.Item].Update(listing.Price, currentTick);
                }
            }
        }
    }
}
```

---

## 4. Trading Mechanics

### 4.1 Direct Trading (Player-to-Agent/Player-to-Player)

```
Process:
  1. Player approaches target within interaction radius (10 meters)
  2. Initiate trade interaction (F key or interaction button)
  3. Trade window opens with split-screen interface
  4. Negotiation phase begins (optional)
  5. Both parties place items/credits in offer area
  6. Both parties click "Accept"
  7. Trade executes atomically (all or nothing)
  8. Items/credits transferred instantly
  9. Reputation updated based on trade fairness

Trade Window UI Layout:
  ┌─────────────────────────────────────────┐
  │  YOUR INVENTORY    │  THEIR INVENTORY   │
  │  [64 slots]        │  [read-only view]  │
  ├─────────────────────────────────────────┤
  │  YOUR OFFER        │  THEIR OFFER       │
  │  [16 slots]        │  [16 slots]        │
  │  Credits: [____]   │  Credits: [read]   │
  ├─────────────────────────────────────────┤
  │  [CANCEL]  [ACCEPT]  [Value: 150¢]      │
  └─────────────────────────────────────────┘
```

**Validation Rules**:
- Both parties must have sufficient inventory space for received items
- Both parties must have sufficient credits for offered amounts
- Trade only completes when both click "Accept"
- Atomic execution: Either all items transfer or none do
- No partial trades allowed

**Cancel Conditions**:
- Either party cancels before acceptance
- Either party moves out of interaction radius (10m)
- Either party takes damage (combat interrupt)
- 5-minute timeout from window opening

**AI Negotiation Behavior**:
```csharp
public float NegotiatePrice(Agent agent, ItemType item, float playerOffer, TradeType type)
{
    var belief = agent.PriceBeliefs[item];
    float expectedPrice = belief.MeanPrice;
    
    // Personality modifiers
    float greedModifier = (agent.Personality.Greed - 50) / 100f;  // -0.5 to +0.5
    float agreeablenessModifier = (agent.Personality.Agreeableness - 50) / 100f;
    
    if (type == TradeType.Sell)  // AI is selling
    {
        float minAcceptable = expectedPrice * (0.9f + greedModifier * 0.2f);
        if (playerOffer >= minAcceptable)
            return playerOffer;  // Accept
        else
            return expectedPrice * (1.1f + greedModifier * 0.1f);  // Counter-offer
    }
    else  // AI is buying
    {
        float maxAcceptable = expectedPrice * (1.1f - greedModifier * 0.2f);
        if (playerOffer <= maxAcceptable)
            return playerOffer;  // Accept
        else
            return expectedPrice * (0.9f - greedModifier * 0.1f);  // Counter-offer
    }
}
```

### 4.2 Store/Shop System

**Creating a Store**:
```
Requirements:
  - Store building or market stall (constructed or rented)
  - Inventory: Owner must stock items for sale
  - Pricing: Owner sets fixed or negotiable prices
  - Capital: Listing fees require 1% of item value/day
  
Setup Process:
  1. Build or rent store location
  2. Access store management interface
  3. Drag items from inventory to store inventory
  4. Set price for each item listing
  5. Choose pricing mode: Fixed, Negotiable, or Auction
  6. Store opens automatically
```

**Store Types**:

| Type | Description | Buyer Experience | Fee Structure |
|------|-------------|------------------|---------------|
| **Fixed Price** | Set price, no negotiation | Pay exactly listed price | Standard 3% sales tax |
| **Negotiable** | Listed price is starting point | Can offer ±20% of listed | +1% negotiation fee |
| **Auction** | Time-limited bidding | Highest bidder wins | 5% auction fee |
| **Bulk** | Volume discounts | Price per unit decreases with quantity | Standard tax |

**Store Fees**:
```
Listing Fee: 1% of item value per day (minimum 1¢)
  - Deducted daily from store owner's account
  - If insufficient funds, items delisted
  - Town center stores: 0.5% (subsidized)
  - Remote stores: 1.5% (higher maintenance)

Sales Tax: Set by jurisdiction (default 3-5%)
  - Deducted automatically at sale
  - 80% to town treasury, 20% to state (if applicable)
  - Applied to final sale price

Auction Fee: 5% of final bid
  - Only for auction-type stores
  - Deducted from seller's proceeds
```

**AI Shopping Behavior**:
```csharp
public class AIShoppingBehavior
{
    public void ShopForNeeds(Agent agent)
    {
        // Identify needs
        var needs = agent.GetCurrentNeeds();
        
        foreach (var need in needs.Where(n => n.Priority > 0.7f))
        {
            // Find stores selling needed items
            var stores = FindStoresSelling(need.ItemType, agent.Location, radius: 500f);
            
            // Evaluate options
            var options = stores.Select(store => new ShoppingOption
            {
                Store = store,
                Price = store.GetPrice(need.ItemType),
                Distance = CalculateDistance(agent.Location, store.Location),
                Reputation = store.Owner.Reputation
            });
            
            // Score each option (price + distance + reputation)
            var bestOption = options
                .OrderBy(o => CalculateShoppingScore(o, agent))
                .FirstOrDefault();
            
            if (bestOption != null && 
                agent.PriceBeliefs[need.ItemType].IsGoodDeal(bestOption.Price, TradeType.Buy))
            {
                // Travel to store and purchase
                agent.SetDestination(bestOption.Store.Location);
                agent.QueueAction(Action.BuyItem, bestOption.Store, need.ItemType);
            }
        }
    }
    
    private float CalculateShoppingScore(ShoppingOption option, Agent agent)
    {
        float priceScore = option.Price / agent.PriceBeliefs[option.Item].MeanPrice;
        float distanceScore = option.Distance / 100f;  // Normalize to 100m
        float reputationScore = (100f - option.Reputation) / 100f;  // Prefer reputable stores
        
        // Weighted combination
        return priceScore * 0.5f + distanceScore * 0.3f + reputationScore * 0.2f;
    }
}
```

### 4.3 Market Stalls (Public Market)

**Location**: Town center designated market area (typically 20×20m)

**Features**:
- Rentable stalls (20-50¢ per day depending on location)
- High visibility to all town visitors
- Bulk trading capabilities
- Price competition with neighboring vendors
- Town guard protection (reduced theft risk)

**Stall Rental Tiers**:

| Tier | Daily Rent | Location | Foot Traffic | Features |
|------|-----------|----------|--------------|----------|
| Corner | 50¢ | Prime corner | Very High | +20% visibility |
| Center | 40¢ | Market center | High | Standard |
| Edge | 30¢ | Market edge | Medium | Standard |
| Entry | 20¢ | Near entrance | Medium | Travelers only |

**Market Day Events**:
- Weekly market day (every 7th day): 2× foot traffic
- Special events: Holiday markets, seasonal fairs
- Auction days: Monthly high-value item auctions

**AI Vendor Behavior**:
- Adjust prices based on neighboring stalls
- Restock based on previous day's sales
- Close stall during non-peak hours to save fees
- Specialize in specific item categories

---

## 5. Contract System

### 5.1 Contract Types

**1. Labor Contract**:
```
Structure:
  Employer: Contract issuer (player or agent)
  Worker: Contract acceptor (player or agent)
  Duration: Hours of labor required
  Tasks: Specific work types (gathering, building, crafting)
  Payment: Amount and timing (upfront, 50/50, completion)
  Penalties: Breach consequences

Standard Terms:
  Payment Options:
    - Upfront: 100% at contract start (risk to employer)
    - 50/50: Half at start, half at completion
    - On Completion: 100% at end (risk to worker)
  
  Breach Penalties:
    - Worker fails to complete: Forfeit 20% of contract value
    - Employer fails to pay: Pay 150% of owed amount
    - Early termination: Pro-rated payment + 10% penalty

Example:
  "Build a workshop (requires 20 wood, 10 stone)"
  Duration: 2 hours
  Payment: 150¢ (50/50 split)
  Penalty: 30¢ for non-completion
```

**2. Delivery Contract**:
```
Structure:
  Shipper: Responsible for transport
  Recipient: Receiving party
  Cargo: Items to be delivered
  Destination: Location for delivery
  Deadline: Time limit for completion
  
Standard Terms:
  Late Delivery: 1% penalty per hour late
  Failed Delivery: 50% penalty, reputation loss
  Proof of Delivery: Digital receipt at destination
  
Example:
  "Deliver 50 iron ingots to Smithy in North District"
  Deadline: 24 hours
  Payment: 80¢ on delivery
  Late Fee: 0.8¢ per hour
```

**3. Construction Contract**:
```
Structure:
  Client: Building owner
  Builder: Construction contractor
  Structure: Specific building type
  Location: Exact placement coordinates
  Milestones: Payment checkpoints
  Quality: Minimum quality requirement
  
Standard Terms:
  Milestone Payments:
    - 25%: Foundation complete
    - 50%: Frame complete
    - 75%: Exterior complete
    - 100%: Final inspection passed
  
  Quality Requirements:
    - Poor quality: Repair or refund
    - Wrong structure type: Full refund + 25% penalty
  
Example:
  "Construct Advanced Workshop at coordinates (X: 150, Z: 200)"
  Materials provided by: Client
  Milestones: 4 payments of 250¢ each
  Time limit: 48 hours
```

**4. Supply Contract (Ongoing)**:
```
Structure:
  Supplier: Provides goods regularly
  Buyer: Purchases on schedule
  Item: Specific resource or product
  Quantity: Per delivery
  Frequency: Daily/weekly
  Duration: Contract length
  
Standard Terms:
  Regular Payments: Automatic at each delivery
  Cancellation: 3-day notice required
  Quality Consistency: ±10% of specified quality
  Price Lock: Fixed for contract duration (or market-based with cap)
  
Example:
  "Supply 20 cooked meals per day for 14 days"
  Price: 8¢ per meal (160¢/day)
  Payment: Daily
  Cancellation: 72-hour notice
```

### 5.2 Contract Negotiation System

```csharp
public class ContractNegotiation
{
    public Guid NegotiationId { get; set; }
    public Guid ProposerId { get; set; }
    public Guid RecipientId { get; set; }
    public ContractTerms CurrentTerms { get; set; }
    public List<ContractTerms> TermHistory { get; set; }
    public NegotiationStatus Status { get; set; }
    public int CreatedTick { get; set; }
    public int ExpiresTick { get; set; }
    
    public ContractNegotiation(ContractTerms initialTerms, Guid proposer, Guid recipient)
    {
        NegotiationId = Guid.NewGuid();
        ProposerId = proposer;
        RecipientId = recipient;
        CurrentTerms = initialTerms;
        TermHistory = new List<ContractTerms> { initialTerms };
        Status = NegotiationStatus.Pending;
        CreatedTick = GameTime.CurrentTick;
        ExpiresTick = CreatedTick + (int)(GameTime.TICKS_PER_HOUR * 24); // 24 hour expiration
    }
    
    public void CounterOffer(ContractTerms counter)
    {
        // Validate counter is within acceptable bounds
        if (!IsValidCounterOffer(counter))
            throw new InvalidOperationException("Counter offer outside acceptable range");
        
        TermHistory.Add(CurrentTerms);
        CurrentTerms = counter;
        Status = NegotiationStatus.Countered;
        
        // Reset expiration
        ExpiresTick = GameTime.CurrentTick + (int)(GameTime.TICKS_PER_HOUR * 24);
    }
    
    public Contract Accept()
    {
        if (Status != NegotiationStatus.Pending && Status != NegotiationStatus.Countered)
            throw new InvalidOperationException("Cannot accept in current state");
        
        var contract = new Contract(CurrentTerms, ProposerId, RecipientId);
        ContractManager.Register(contract);
        
        Status = NegotiationStatus.Accepted;
        return contract;
    }
    
    public void Reject()
    {
        Status = NegotiationStatus.Rejected;
    }
    
    public void Cancel()
    {
        Status = NegotiationStatus.Cancelled;
    }
    
    private bool IsValidCounterOffer(ContractTerms counter)
    {
        var original = TermHistory.First();
        
        // Payment must be within 30% of original
        float paymentDiff = Mathf.Abs(counter.Payment - original.Payment) / original.Payment;
        if (paymentDiff > 0.3f) return false;
        
        // Duration within 50% of original
        float durationDiff = Mathf.Abs(counter.Duration - original.Duration) / original.Duration;
        if (durationDiff > 0.5f) return false;
        
        return true;
    }
}

public enum NegotiationStatus
{
    Pending,      // Awaiting response
    Countered,    // Terms modified
    Accepted,     // Agreement reached
    Rejected,     // Declined
    Cancelled,    // Withdrawn
    Expired       // Time limit exceeded
}
```

### 5.3 AI Contract Evaluation

```csharp
public class AIContractEvaluator
{
    public float EvaluateContract(Agent agent, ContractTerms terms, ContractType type)
    {
        float score = 0.0f;
        
        // 1. Profitability Analysis
        float expectedCost = CalculateOpportunityCost(agent, terms, type);
        float profitMargin = (terms.Payment - expectedCost) / expectedCost;
        score += profitMargin * 50f;  // Weight: 50 points max
        
        // 2. Feasibility Check
        float feasibility = CheckFeasibility(agent, terms, type);
        if (feasibility < 0.8f)
            return 0.0f;  // Cannot complete, reject
        score += feasibility * 20f;  // Weight: 20 points
        
        // 3. Employer Reputation
        var employer = AgentManager.GetAgent(terms.EmployerId);
        float reputationScore = (employer.Reputation + 100) / 200f;  // Normalize to 0-1
        score += reputationScore * 15f;  // Weight: 15 points
        
        // 4. Risk Assessment
        float riskScore = AssessRisk(agent, terms, type);
        score += (1 - riskScore) * 15f;  // Weight: 15 points (lower risk = higher score)
        
        return score;
    }
    
    public bool ShouldAcceptContract(Agent agent, float score)
    {
        // Acceptance thresholds based on agent personality
        float baseThreshold = 60.0f;
        
        // Conscientious agents more selective
        baseThreshold += (agent.Personality.Conscientiousness - 50) / 10f;
        
        // Greedy agents accept lower scores for high pay
        if (agent.Personality.Greed > 70)
            baseThreshold -= 10f;
        
        return score >= baseThreshold;
    }
    
    private float CalculateOpportunityCost(Agent agent, ContractTerms terms, ContractType type)
    {
        // Calculate what the agent could earn doing other activities
        float hourlyRate = agent.Skills.GetHourlyRateFor(type);
        float timeRequired = terms.Duration;
        float alternativeEarnings = hourlyRate * timeRequired;
        
        // Add material costs if applicable
        float materialCosts = terms.RequiredMaterials?.Sum(m => m.Value * GetMarketPrice(m.Key)) ?? 0;
        
        return alternativeEarnings + materialCosts;
    }
    
    private float CheckFeasibility(Agent agent, ContractTerms terms, ContractType type)
    {
        // Check if agent has required skills
        var requiredSkills = GetRequiredSkills(type);
        foreach (var skill in requiredSkills)
        {
            if (agent.Skills[skill] < terms.MinimumSkillLevel)
                return 0.0f;
        }
        
        // Check inventory capacity for delivery contracts
        if (type == ContractType.Delivery)
        {
            float totalWeight = terms.Items.Sum(i => i.Weight);
            if (totalWeight > agent.Inventory.MaxWeight)
                return 0.5f;  // Can do it but needs multiple trips
        }
        
        // Check if agent has materials for construction
        if (type == ContractType.Construction && terms.MaterialsProvidedBy == Provider.Contractor)
        {
            foreach (var material in terms.RequiredMaterials)
            {
                if (agent.Inventory.Count(material.Key) < material.Value)
                    return 0.0f;
            }
        }
        
        return 1.0f;
    }
}
```

---

## 6. Banking & Credit

### 6.1 Banking System

**Bank Locations**: Town halls, major public buildings, standalone bank structures

**Account Features**:
```
Deposit/Withdrawal:
  - Instant for digital currency
  - 1% conversion fee for physical → digital
  - No fee for digital → physical
  - Minimum deposit: 1¢
  - Maximum balance: 1,000,000¢ (soft cap)

Interest (Savings Accounts):
  - Rate: 0.5% per game week (28 real hours)
  - Compounding: Weekly
  - Minimum balance for interest: 100¢
  - Maximum interest-earning balance: 50,000¢
  
Transfers:
  - Same jurisdiction: Instant, no fee
  - Cross-jurisdiction: 2% fee, 1-hour delay
  - Minimum transfer: 1¢
```

**Account Types**:

| Type | Features | Requirements | Fees |
|------|----------|--------------|------|
| Basic | Deposit, withdraw, transfer | None | None |
| Savings | +0.5% weekly interest | Min balance 100¢ | None |
| Business | +Bulk transfers, statements | Town registered business | 5¢/month |
| Premium | +Priority service, loans | 1,000¢+ balance | 10¢/month |

### 6.2 Loan Mechanics

```
Loan Terms:
  Maximum Loan Amount: 5 × current wealth (assets + credits)
  Interest Rate: 2% per game week
  Collateral Requirements:
    - < 500¢ loan: No collateral (credit score only)
    - 500-2,000¢: Property or items (100% value)
    - 2,000-5,000¢: Property (150% value)
    - 5,000¢+: Property + guarantor
  
  Repayment Schedule:
    - Weekly payments required
    - Minimum payment: Interest + 5% principal
    - Early repayment: No penalty
    
  Default Consequences:
    - Week 1 late: Warning, reputation -5
    - Week 2 late: Penalty fee (5% of owed)
    - Week 3 late: Collateral seizure begins
    - Week 4 late: All collateral seized, credit score reset
```

**Credit Score System**:
```csharp
public class CreditScore
{
    public Guid AgentId { get; set; }
    public int Score { get; set; }  // 0-1000 scale
    public int HistoryLength { get; set; }  // Weeks of history
    public List<LoanRecord> LoanHistory { get; set; }
    
    public void UpdateScore()
    {
        int newScore = 500;  // Baseline
        
        // Payment history (40% weight)
        var onTimePayments = LoanHistory.Count(l => l.PaidOnTime);
        var totalPayments = LoanHistory.Count();
        if (totalPayments > 0)
            newScore += (int)((onTimePayments / (float)totalPayments) * 400);
        
        // Credit utilization (30% weight)
        float utilization = GetCurrentUtilization();
        newScore += (int)((1 - utilization) * 300);
        
        // History length (20% weight)
        newScore += Mathf.Min(HistoryLength * 2, 200);
        
        // Reputation impact (10% weight)
        var agent = AgentManager.GetAgent(AgentId);
        newScore += (int)((agent.Reputation + 100) / 200f * 100);
        
        Score = Mathf.Clamp(newScore, 0, 1000);
    }
    
    public float GetInterestRateModifier()
    {
        // Lower score = higher rates
        if (Score >= 800) return 0.5f;   // 50% of base rate
        if (Score >= 600) return 0.75f;  // 75% of base rate
        if (Score >= 400) return 1.0f;   // Base rate
        if (Score >= 200) return 1.5f;   // 150% of base rate
        return 2.0f;  // 200% of base rate (high risk)
    }
    
    public int GetMaxLoanAmount(int currentWealth)
    {
        int multiplier = Score switch
        {
            >= 800 => 10,
            >= 600 => 7,
            >= 400 => 5,
            >= 200 => 3,
            _ => 1
        };
        
        return currentWealth * multiplier;
    }
}
```

---

## 7. Taxation

### 7.1 Tax Types

**1. Sales Tax (Transaction Tax)**:
```
Application: All store purchases and market transactions
Rate: Set by jurisdiction (default 3-10%, max 50% per technical-constants.md)
Collection: Automatic at point of sale
Distribution: 
  - 80% to town treasury
  - 20% to state treasury (if part of state)
  
Example:
  Item price: 100¢
  Tax rate: 5%
  Tax amount: 5¢
  Buyer pays: 105¢
  Seller receives: 100¢
  Treasury receives: 5¢
```

**2. Income Tax**:
```
Application: Contract payments and service fees
Rate: Progressive brackets
  - 0-100¢: 0%
  - 100-500¢: 5%
  - 500-1,000¢: 10%
  - 1,000-5,000¢: 15%
  - 5,000¢+: 20%
  
Collection: Deducted from payment before receipt
Distribution: 100% to jurisdiction treasury

Example:
  Contract payment: 800¢
  Tax calculation:
    - First 100¢: 0% = 0¢
    - Next 400¢: 5% = 20¢
    - Next 300¢: 10% = 30¢
  Total tax: 50¢
  Worker receives: 750¢
```

**3. Property Tax**:
```
Application: All land claims
Rate: 1-5% of claim value per year (set by jurisdiction)
Collection: Annual, can be paid quarterly
Unpaid Consequences:
  - 30 days overdue: Warning + 10% penalty
  - 60 days overdue: Lien placed on property
  - 90 days overdue: Property auctioned to cover tax debt
  
Example:
  Claim value: 1,000¢
  Tax rate: 2%
  Annual tax: 20¢
  Quarterly payment: 5¢
```

**4. Import/Export Tax**:
```
Application: Goods crossing jurisdiction borders
Rate: Variable by good type (0-20%)
  - Raw materials: 0-5%
  - Processed goods: 5-10%
  - Luxury items: 10-20%
  
Enforcement: Checkpoints at jurisdiction boundaries
Exemptions: Personal items (up to 100¢ value)

Example:
  Transporting 500¢ worth of iron ingots across border
  Tax rate: 5%
  Tax due: 25¢ at checkpoint
```

### 7.2 Tax Collection Implementation

```csharp
public class TaxCollectionSystem
{
    public void CollectSalesTax(Transaction transaction, Jurisdiction jurisdiction)
    {
        float taxRate = jurisdiction.GetTaxRate(TaxType.Sales, transaction.ItemCategory);
        float taxAmount = transaction.Amount * taxRate;
        
        // Deduct from transaction amount
        transaction.TaxCollected = taxAmount;
        transaction.NetAmount = transaction.Amount - taxAmount;
        
        // Distribute to treasuries
        float townShare = taxAmount * 0.8f;
        float stateShare = taxAmount * 0.2f;
        
        jurisdiction.TownTreasury.Add(townShare);
        if (jurisdiction.State != null)
            jurisdiction.State.Treasury.Add(stateShare);
        
        // Record for transparency
        jurisdiction.RecordTaxCollection(new TaxRecord
        {
            Type = TaxType.Sales,
            Amount = taxAmount,
            Source = transaction,
            Timestamp = GameTime.CurrentTick,
            Distribution = new Dictionary<string, float>
            {
                { "Town", townShare },
                { "State", stateShare }
            }
        });
    }
    
    public void CollectIncomeTax(Payment payment, Jurisdiction jurisdiction)
    {
        float taxRate = GetProgressiveRate(payment.Amount);
        float taxAmount = payment.Amount * taxRate;
        
        // Deduct before recipient receives
        payment.TaxWithheld = taxAmount;
        payment.NetAmount = payment.Amount - taxAmount;
        
        jurisdiction.Treasury.Add(taxAmount);
        
        // Update tax records for recipient
        var recipient = AgentManager.GetAgent(payment.RecipientId);
        recipient.TaxRecords.Add(new TaxRecord
        {
            Type = TaxType.Income,
            Amount = taxAmount,
            GrossIncome = payment.Amount,
            Timestamp = GameTime.CurrentTick
        });
    }
    
    private float GetProgressiveRate(float amount)
    {
        // Progressive tax brackets
        if (amount <= 100) return 0.0f;
        if (amount <= 500) return 0.05f;
        if (amount <= 1000) return 0.10f;
        if (amount <= 5000) return 0.15f;
        return 0.20f;
    }
}
```

### 7.3 Tax Evasion & Enforcement

**Detection Methods**:
```
Random Audits:
  - 5% of transactions audited automatically
  - Focus on high-value transactions (500¢+)
  - Focus on repeat offenders
  
Discrepancy Detection:
  - Inventory tracking vs reported sales
  - Bank deposits vs claimed income
  - Property ownership vs tax payments
  
Whistleblower System:
  - Players/agents can report suspected evasion
  - Reward: 20% of recovered tax
  - False reports: -10 reputation penalty
```

**Penalty Structure**:

| Offense | Penalty | Additional Consequences |
|---------|---------|------------------------|
| First | 2× owed tax | Warning record |
| Second | 3× owed tax + 100¢ fine | Reputation -15 |
| Third | 4× owed tax + 500¢ fine + 12-hour jail | Reputation -30 |
| Fourth+ | All assets seized + ban from public office | Permanent record |

**Black Market**:
```
Characteristics:
  - Untaxed trading (0% tax)
  - No legal protections
  - Risk of detection (15% per transaction)
  - Lower prices possible (seller keeps full amount)
  - No reputation gain from trades
  
Black Market Locations:
  - Remote areas (outside town centers)
  - Abandoned structures
  - Designated "gray markets" (semi-legal)
  
Risks:
  - 15% chance of detection per transaction
  - If detected: Tax + 100% penalty
  - Scams more common (no legal recourse)
  - Quality issues (no standards enforcement)
```

---

## 8. Economic Indicators

### 8.1 Dashboard Metrics

Available to all citizens via town hall terminals or governance interface:

**Primary Indicators**:

| Metric | Calculation | Update Frequency | Target Range |
|--------|-------------|------------------|--------------|
| **GDP** | Total transaction value (daily) | Daily | Growth: 2-5%/week |
| **Inflation Rate** | Price index change | Weekly | 2-5% per year |
| **Unemployment** | % agents without active contracts | Daily | < 20% |
| **Trade Volume** | Number of transactions | Daily | Context dependent |
| **Avg Wage** | Credits per labor hour | Weekly | 40-60¢ |
| **Wealth Distribution** | Gini coefficient | Weekly | 0.3-0.5 (fair) |
| **Currency Supply** | Total credits in circulation | Daily | Context dependent |

**Secondary Indicators**:

| Metric | Description | Use Case |
|--------|-------------|----------|
| Resource Scarcity | Index per resource type | Production planning |
| Market Velocity | Transactions per agent per day | Economic activity level |
| Price Volatility | Standard deviation of prices | Market stability |
| Tax Revenue | Daily treasury income | Budget planning |
| Trade Balance | Imports vs exports | Self-sufficiency |

### 8.2 Economic Indicators Implementation

```csharp
public class EconomicDashboard
{
    private Dictionary<string, EconomicIndicator> _indicators;
    private CircularBuffer<Transaction> _recentTransactions;
    
    public void UpdateIndicators(int currentTick)
    {
        // Update daily
        if (currentTick % GameTime.TICKS_PER_DAY == 0)
        {
            UpdateGDP();
            UpdateUnemployment();
            UpdateTradeVolume();
            UpdateCurrencySupply();
        }
        
        // Update weekly
        if (currentTick % (GameTime.TICKS_PER_DAY * 7) == 0)
        {
            UpdateInflationRate();
            UpdateAverageWage();
            UpdateWealthDistribution();
        }
    }
    
    private void UpdateGDP()
    {
        var last24Hours = _recentTransactions.Where(t => 
            t.Timestamp >= GameTime.CurrentTick - GameTime.TICKS_PER_DAY);
        
        float gdp = last24Hours.Sum(t => t.Amount);
        _indicators["GDP"].SetValue(gdp);
        
        // Calculate growth rate
        if (_indicators["GDP"].History.Count > 7)
        {
            float lastWeek = _indicators["GDP"].GetValue(7);
            float growthRate = (gdp - lastWeek) / lastWeek;
            _indicators["GDP_Growth"].SetValue(growthRate);
        }
    }
    
    private void UpdateInflationRate()
    {
        // Track basket of 20 common goods
        var basket = GetPriceBasket();
        float currentIndex = CalculatePriceIndex(basket);
        float lastWeekIndex = GetPriceIndexFromHistory(7);
        
        float inflationRate = (currentIndex - lastWeekIndex) / lastWeekIndex;
        _indicators["Inflation"].SetValue(inflationRate);
        
        // Annualize (multiply by 52 weeks)
        _indicators["Inflation_Annualized"].SetValue(inflationRate * 52);
    }
    
    private void UpdateWealthDistribution()
    {
        var wealths = AgentManager.AllAgents.Select(a => a.TotalWealth).OrderBy(w => w).ToList();
        float gini = CalculateGiniCoefficient(wealths);
        _indicators["Gini_Coefficient"].SetValue(gini);
        
        // Classification
        string status = gini switch
        {
            < 0.3f => "Equal",
            < 0.5f => "Fair",
            < 0.7f => "Unequal",
            _ => "Severely Unequal"
        };
        _indicators["Distribution_Status"].SetValue(status);
    }
    
    private float CalculateGiniCoefficient(List<float> values)
    {
        int n = values.Count;
        if (n == 0) return 0;
        
        float sum = values.Sum();
        float mean = sum / n;
        
        float absoluteDiffSum = 0;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                absoluteDiffSum += Math.Abs(values[i] - values[j]);
            }
        }
        
        return absoluteDiffSum / (2 * n * n * mean);
    }
}
```

### 8.3 Economic Cycles

**Boom Phase**:
```
Indicators:
  - GDP growth > 5% weekly
  - Unemployment < 10%
  - Rising prices (3-8% inflation)
  - High construction activity
  - Increased credit usage
  
Triggers:
  - Discovery of new resource deposits
  - Population growth (new agents)
  - Technological breakthroughs
  - Successful trade agreements
  
Duration: 7-21 days
Consequences:
  - Wealth accumulation
  - Inflation pressure
  - Resource depletion acceleration
```

**Recession Phase**:
```
Indicators:
  - GDP decline or stagnation
  - Unemployment > 30%
  - Falling prices (deflation risk)
  - Reduced construction
  - Credit defaults increase
  
Triggers:
  - Resource depletion
  - Natural disasters
  - Trade route disruptions
  - Political instability
  - Tax overreach
  
Duration: 7-14 days
Consequences:
  - Wealth destruction
  - Business failures
  - Government intervention likely
```

**Recovery Phase**:
```
Indicators:
  - Stabilizing prices (1-3% inflation)
  - Gradual unemployment decrease
  - New industries emerging
  - Innovation activity
  - Cautious investment
  
Characteristics:
  - New economic patterns emerge
  - Specialization shifts
  - Different resources in demand
  - New trade relationships form
  
Duration: 14-28 days
```

**Cycle Management**:
```csharp
public class EconomicCycleManager
{
    public EconomicPhase CurrentPhase { get; private set; }
    public int PhaseDuration { get; private set; }
    
    public void DetectPhaseTransition(EconomicIndicators indicators)
    {
        var newPhase = AnalyzeIndicators(indicators);
        
        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            PhaseDuration = 0;
            OnPhaseChanged(newPhase);
        }
        else
        {
            PhaseDuration++;
        }
    }
    
    public void RecommendPolicy(EconomicIndicators indicators)
    {
        switch (CurrentPhase)
        {
            case EconomicPhase.Boom:
                if (indicators.Inflation > 0.08f)
                {
                    Recommend("Increase tax rates to cool economy");
                    Recommend("Reduce government spending");
                }
                break;
                
            case EconomicPhase.Recession:
                if (indicators.Unemployment > 0.3f)
                {
                    Recommend("Increase welfare programs");
                    Recommend("Reduce tax rates");
                    Recommend("Infrastructure spending projects");
                }
                break;
                
            case EconomicPhase.Recovery:
                Recommend("Maintain stable policy");
                Recommend("Support emerging industries");
                break;
        }
    }
}
```

---

## Appendix A: Cross-References

### Technical Constants Integration

All economic values reference `planning/meta/technical-constants.md`:

| This Document | Technical Constant | Value |
|---------------|-------------------|-------|
| Starting player credits | STARTING_CREDITS_PLAYER | 100¢ |
| Starting agent credits | STARTING_CREDITS_AGENT | 100¢ |
| Wood stack size | STACK_SIZE_WOOD | 100 |
| Stone stack size | STACK_SIZE_STONE | 50 |
| Food stack size | STACK_SIZE_FOOD | 20 |
| Tool stack size | STACK_SIZE_TOOLS | 1 (non-stackable) |
| Max inventory slots | INVENTORY_SLOTS_PLAYER/AGENT | 64 |
| Agent population MVP | AGENTS_MVP | 25 |
| Agent population Post-MVP | AGENTS_POST_MVP | 100 |
| Price Day 1 Food | PRICE_DAY1_FOOD_MIN/MAX | 5-15¢ |
| Price Day 1 Wood | PRICE_DAY1_WOOD_MIN/MAX | 3-8¢ |
| Price Day 1 Tools | PRICE_DAY1_TOOLS_MIN/MAX | 50-150¢ |
| Tax rate max | TAX_RATE_MAX_PERCENT | 50% |
| Tax rate default | TAX_RATE_DEFAULT_PERCENT | 10% |
| Game day length | DAY_LENGTH_REAL_MINUTES | 60 minutes |
| Year length | YEAR_LENGTH_DAYS | 28 days |
| Quality levels | QUALITY_* constants | 5 tiers |

### Session Dependencies

- **Session 2 - 02-economic-behavior.md**: AI economic decision-making
- **Session 2 - 01-core-ai-architecture.md**: Agent capabilities for trading
- **Session 3 - 01-gameplay-systems-architecture.md**: Core systems integration
- **Session 5 - Governance Mechanics**: Tax laws, economic policy

---

## Appendix B: Quick Reference Tables

### Price Conversion Matrix

| Item | Day 1 | Day 7 | Day 30 | % Change D1→D30 |
|------|-------|-------|--------|-----------------|
| Wood | 5¢ | 4¢ | 2.5¢ | -50% |
| Stone | 3¢ | 2¢ | 1.5¢ | -50% |
| Iron Ingot | 15¢ | 12¢ | 10.5¢ | -30% |
| Food (cooked) | 10¢ | 8¢ | 7¢ | -30% |
| Iron Tool | 100¢ | 80¢ | 50¢ | -50% |
| Labor (hour) | N/A | 50¢ | 150¢ | +200% (D7→D30) |

### Tax Quick Reference

| Tax Type | Rate Range | Collection Point | Who Sets |
|----------|-----------|------------------|----------|
| Sales | 3-10% | Point of sale | Town/State |
| Income | 0-20% | Progressive | State only |
| Property | 1-5%/year | Annual | Town only |
| Import/Export | 0-20% | Border checkpoint | Both |

### Contract Quick Reference

| Type | Min Value | Max Value | Duration | Payment |
|------|-----------|-----------|----------|---------|
| Labor | 10¢ | 500¢ | 1-8 hours | Variable |
| Delivery | 20¢ | 300¢ | 1-48 hours | On delivery |
| Construction | 100¢ | 5,000¢ | 1-7 days | Milestones |
| Supply | 50¢/delivery | 500¢/delivery | 7-30 days | Per delivery |

---

**END OF DOCUMENT**

*Document Version: 1.0*  
*Review Cycle: Check against technical-constants.md updates*  
*Next Review Date: 2026-02-15*
