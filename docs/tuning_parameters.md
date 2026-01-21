# Tuning Parameters Reference

This document describes all configurable tuning parameters for the Societies simulation. Parameters are loaded from `config/tuning.json` and validated by the `TuningConfig` class.

---

## World & Time

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `world_w` | int | 96 | 16-512 | World width in tiles |
| `world_h` | int | 96 | 16-512 | World height in tiles |
| `ticks_per_day` | int | 24 | 1-100 | Simulation ticks per day |

---

## Agent Needs

### Hunger
| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `hunger_drain_per_tick` | float | 0.7 | 0-10 | Hunger drain per tick |
| `eat_threshold` | float | 50 | 0-100 | Hunger below which agents seek food |
| `urgent_hunger_threshold` | float | 25 | 0-100 | Hunger for urgent food needs |
| `emergency_hunger_threshold` | float | 15 | 0-100 | Hunger for emergency eating |
| `emergency_food_hunger_threshold` | float | 40 | 0-100 | Hunger to trigger emergency food acquisition |
| `berry_nutrition` | float | 20 | 0-100 | Hunger restored by berries |
| `meal_nutrition` | float | 40 | 0-100 | Hunger restored by cooked meal |
| `initial_hunger` | float | 100 | 0-100 | Starting hunger level |

### Stamina
| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `stamina_max` | float | 100 | 1-1000 | Maximum stamina |
| `stamina_drain_gather` | float | 2.0 | 0-50 | Stamina cost for gathering |
| `stamina_drain_move` | float | 0.5 | 0-10 | Stamina cost for movement |
| `stamina_drain_craft` | float | 3.0 | 0-50 | Stamina cost for crafting |
| `stamina_drain_claim` | float | 2.0 | 0-50 | Stamina cost for claiming |
| `stamina_drain_build` | float | 5.0 | 0-50 | Stamina cost for building |
| `stamina_recover_rest` | float | 5.0 | 0-50 | Stamina recovered when resting |
| `stamina_recover_sleep` | float | 10.0 | 0-50 | Stamina recovered when sleeping |
| `stamina_low_threshold` | float | 20.0 | 0-100 | Stamina below which agent is exhausted |
| `stamina_exhausted_yield_penalty` | float | 0.5 | 0-1 | Yield penalty when exhausted |
| `stamina_rest_optimization_threshold` | float | 50 | 0-100 | Stamina for optional rest |
| `initial_stamina` | float | 100 | 0-100 | Starting stamina level |

---

## Starting Conditions

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `starting_money` | int | 200 | 0-10000 | Starting money for agents |
| `starting_berries` | int | 4 | 0-100 | Starting berries for agents |
| `initial_skill_level` | float | 1.0 | 0-10 | Initial skill level |
| `npc_count` | int | 10 | 0-1000 | Number of NPCs to spawn |
| `starting_agents_claim_count` | int | 5 | 0-100 | Agents given starting claims |

---

## Resource Nodes

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `berry_nodes_count` | int | 50 | 0-1000 | Number of berry nodes |
| `tree_nodes_count` | int | 70 | 0-1000 | Number of tree nodes |
| `ore_nodes_count` | int | 20 | 0-500 | Number of ore nodes |
| `berry_regen_per_day` | int | 3 | 0-100 | Berry regeneration per day |
| `tree_regen_per_day` | int | 2 | 0-100 | Tree regeneration per day |
| `berry_max_stock` | int | 10 | 1-100 | Max stock for berry nodes |
| `tree_max_stock` | int | 20 | 1-100 | Max stock for tree nodes |
| `ore_max_stock` | int | 30 | 1-100 | Max stock for ore nodes |
| `node_spawn_attempts` | int | 100 | 1-1000 | Max attempts to spawn a node |

---

## Pollution

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `pollution_impact` | float | 0.5 | 0-2 | Pollution impact on resource regen |
| `pollution_decay_per_day` | float | 0.05 | 0-1 | Pollution decay rate per day |
| `pollution_per_ore` | float | 0.01 | 0-1 | Pollution added per ore mined |
| `pollution_high_threshold` | float | 0.6 | 0-1 | Threshold for high pollution |
| `pollution_collapse_threshold` | float | 0.8 | 0-1 | Pollution level causing collapse |
| `food_yield_pollution_start` | float | 0.3 | 0-1 | Pollution where food yield decreases |
| `food_yield_pollution_step` | float | 0.1 | 0-1 | Pollution step for food yield reduction |
| `hunger_drain_pollution_mult` | float | 0.5 | 0-5 | Hunger drain multiplier from pollution |

---

## Market

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `market_pos_x` | int | 48 | 0-512 | Market X position |
| `market_pos_y` | int | 48 | 0-512 | Market Y position |
| `market_match_interval_ticks` | int | 1 | 1-100 | Ticks between market matching |
| `order_ttl_ticks` | int | 48 | 1-1000 | Order time-to-live in ticks |
| `price_ema_alpha` | float | 0.2 | 0-1 | EMA alpha for price smoothing |
| `bid_scarcity_strength` | float | 0.6 | 0-2 | Bid price scarcity sensitivity |
| `ask_surplus_strength` | float | 0.6 | 0-2 | Ask price surplus sensitivity |
| `min_price` | int | 1 | 1-10000 | Minimum price |
| `max_price` | int | 1000 | 1-100000 | Maximum price |
| `target_food_buffer` | int | 5 | 0-100 | Target food inventory buffer |
| `food_scarcity_multiplier` | float | 0.4 | 0-2 | Price multiplier during scarcity |
| `berry_scarcity_threshold` | int | 100 | 0-10000 | Berry stock scarcity threshold |
| `tree_scarcity_threshold` | int | 150 | 0-10000 | Tree stock scarcity threshold |

### Selling Thresholds
| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `sell_surplus_food_over` | int | 6 | 0-100 | Sell berries when above |
| `sell_logs_over` | int | 3 | 0-100 | Sell logs when above |
| `sell_ore_over` | int | 2 | 0-100 | Sell ore when above |
| `sell_planks_over` | int | 5 | 0-100 | Sell planks when above |
| `sell_meals_over` | int | 3 | 0-100 | Sell meals when above |
| `miner_sell_metal_over` | int | 3 | 0-100 | Miners sell metal when above |

---

## Workshops & Crafting

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `workshop_start_count` | int | 1 | 0-100 | Initial workshops |
| `workshop_build_planks` | int | 10 | 0-100 | Planks to build workshop |
| `workshop_build_ticks` | int | 80 | 0-1000 | Ticks to build workshop |
| `workshop_build_min_wealth` | int | 150 | 0-10000 | Min wealth to build |
| `workshop_nearby_radius` | int | 20 | 1-100 | Nearby workshop check radius |
| `crafter_ratio` | float | 0.3 | 0-1 | Population ratio of crafters |
| `crafter_planks_buffer` | int | 10 | 0-100 | Target planks for crafters |
| `crafter_meal_buffer` | int | 6 | 0-100 | Target meals for crafters |
| `craft_profit_margin` | float | 1.15 | 0-5 | Min profit margin to craft |
| `axe_tree_bonus` | int | 2 | 1-10 | Extra yield with axe |
| `pickaxe_ore_bonus` | int | 2 | 1-10 | Extra yield with pickaxe |

---

## Agent Roles

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `tool_acquisition_priority_threshold` | int | 60 | 0-100 | Hunger above which to get tools |
| `miner_pollution_tolerance` | float | 0.8 | 0-1 | Miner pollution tolerance |
| `trader_min_inventory_value` | int | 100 | 0-10000 | Min inventory value for traders |
| `role_switch_interval_ticks` | int | 100 | 1-1000 | Ticks between role evaluation |

---

## Skills

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `skill_xp_per_action` | float | 0.1 | 0-10 | XP gained per action |
| `skill_level_xp_requirement` | float | 100 | 1-10000 | XP needed to level up |

---

## Contracts

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `daily_contract_post_chance` | float | 0.15 | 0-1 | Daily chance to post contract |
| `contract_deadline_days` | int | 2 | 1-100 | Contract deadline in days |
| `contract_payout_multiplier` | float | 1.2 | 0-10 | Contract payout multiplier |
| `contract_accept_min_profit` | int | 5 | 0-1000 | Minimum profit to accept |
| `max_active_contracts_per_agent` | int | 1 | 0-10 | Max contracts per agent |
| `contract_food_focus_multiplier` | float | 2.0 | 0-10 | Food payout during scarcity |
| `contract_payout_multiplier_cap` | float | 2.5 | 0-10 | Max payout multiplier |

---

## Claims & Enforcement

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `claim_cost_coins` | int | 20 | 0-1000 | Cost to claim a tile |
| `claim_ticks` | int | 40 | 1-1000 | Ticks to complete claim |
| `claim_search_radius` | int | 8 | 1-100 | Claim target search radius |
| `claim_min_nearby_resources` | int | 3 | 0-100 | Min resources to justify claim |
| `tile_claim_search_radius` | int | 3 | 1-20 | Tile value calculation radius |
| `tile_value_berry_weight` | float | 2.0 | 0-10 | Berry value weight |
| `tile_value_tree_weight` | float | 1.0 | 0-10 | Tree value weight |
| `tile_value_ore_weight` | float | 1.5 | 0-10 | Ore value weight |
| `tile_value_pollution_penalty` | float | 0.5 | 0-2 | Pollution value penalty |
| `detect_chance` | float | 0.8 | 0-1 | Chance to detect violation |
| `fine_base` | int | 10 | 0-1000 | Base fine amount |
| `min_fine` | int | 5 | 0-1000 | Minimum fine |
| `max_fine` | int | 50 | 0-10000 | Maximum fine |
| `fine_step` | int | 5 | 0-100 | Fine adjustment step |
| `market_ban_days_on_repeat` | int | 2 | 0-100 | Ban days for repeat offenders |
| `repeat_threshold` | int | 3 | 1-100 | Violations for repeat status |
| `violation_window_days` | int | 2 | 1-100 | Window to count violations |
| `harvest_permit_required_default` | bool | true | - | Default harvest permit req |
| `build_permit_required_default` | bool | true | - | Default build permit req |
| `sales_tax_rate_default` | int | 5 | 0-100 | Default sales tax rate |

---

## Factions

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `faction_found_min_money` | int | 80 | 0-10000 | Min money to found faction |
| `faction_found_min_grievance` | float | 0.5 | 0-1 | Min grievance to found |
| `faction_found_daily_chance` | float | 0.05 | 0-1 | Daily chance to found |
| `faction_found_claim_radius` | int | 10 | 1-100 | Founding claim search radius |
| `faction_found_treasury_seed` | int | 50 | 0-10000 | Initial treasury contribution |
| `faction_found_grievance_reduction` | float | 0.3 | 0-1 | Grievance reduction on founding |
| `faction_join_grievance_reduction` | float | 0.2 | 0-1 | Grievance reduction on joining |
| `faction_join_claims_score` | float | 0.4 | 0-2 | Join score for claims |
| `faction_join_scarcity_bonus` | float | 0.3 | 0-2 | Join score during scarcity |
| `faction_join_member_bonus` | float | 0.02 | 0-1 | Join score per member |
| `join_search_radius` | int | 15 | 1-200 | Faction search radius |
| `join_min_score` | float | 0.3 | 0-2 | Min score to join |
| `min_claims_for_join_benefit` | int | 3 | 0-100 | Min claims for join benefit |
| `faction_claims_per_day` | int | 2 | 0-100 | Claims per day |
| `faction_claim_cost` | int | 15 | 0-1000 | Cost per faction claim |
| `faction_eval_interval_ticks` | int | 50 | 1-1000 | Ticks between faction eval |

---

## Governance

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `proposal_grievance_threshold` | float | 0.3 | 0-1 | Grievance to submit proposal |
| `max_proposals_per_day_per_faction` | int | 1 | 0-100 | Max proposals per day |
| `tax_step` | int | 2 | 0-50 | Tax rate change step |
| `vote_grievance_threshold` | float | 0.2 | 0-1 | Grievance to vote for reduction |
| `vote_eco_concern_threshold` | float | 0.6 | 0-1 | Eco concern for stricter laws |
| `grievance_decay_daily` | float | 0.1 | 0-1 | Daily grievance decay |
| `grievance_fine_increase` | float | 0.15 | 0-1 | Grievance increase from fine |
| `grievance_blocked_increase` | float | 0.1 | 0-1 | Grievance from blocked action |

---

## Trade Policy

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `default_relation_policy` | string | "open" | - | Default trade relation |
| `default_relation_tariff_rate` | int | 0 | 0-100 | Default tariff rate |
| `default_factionless_policy` | string | "tariff" | - | Policy toward factionless |
| `default_factionless_tariff_rate` | int | 5 | 0-100 | Factionless tariff rate |
| `tariff_rate_min` | int | 0 | 0-100 | Minimum tariff |
| `tariff_rate_max` | int | 30 | 0-100 | Maximum tariff |
| `policy_change_tariff_step` | int | 5 | 0-50 | Tariff change step |

---

## Collapse Detection

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `collapse_days_window` | int | 5 | 1-100 | Days for collapse detection |
| `starvation_collapse_ratio` | float | 0.2 | 0-1 | Death ratio for collapse |
| `inflation_threshold` | float | 50.0 | 0-1000 | Price threshold for collapse |

---

## Exploration & Memory

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `exploration_trigger_radius` | int | 20 | 1-200 | Radius before exploring |
| `exploration_step_distance` | int | 5 | 1-50 | Distance per explore step |
| `gather_radius_normal` | int | 15 | 1-200 | Normal gather radius |
| `gather_radius_scarce` | int | 50 | 1-500 | Gather radius during scarcity |
| `tree_radius_normal` | int | 15 | 1-200 | Normal tree radius |
| `tree_radius_scarce` | int | 40 | 1-500 | Tree radius during scarcity |
| `memory_capacity` | int | 20 | 1-1000 | Max remembered locations |
| `social_trust_initial` | float | 0.5 | 0-1 | Initial trust for strangers |
| `social_trust_trade_increase` | float | 0.05 | 0-1 | Trust increase per trade |

---

## Metrics

| Key | Type | Default | Range | Description |
|-----|------|---------|-------|-------------|
| `metrics_enabled` | bool | true | - | Whether to collect metrics |
