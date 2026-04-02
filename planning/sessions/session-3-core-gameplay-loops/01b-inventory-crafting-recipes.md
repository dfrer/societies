# Recipe and Crafting Tree Specification

**Status**: Draft  
**Session**: 3 - Core Gameplay Loops  
**Related Documents**:
- `planning/meta/technical-constants.md` (stack sizes, production times, tool durability)
- `01e-inventory-system-spec.md` (inventory mechanics)
- `01d-tool-system-spec.md` (tool system)

---

## 1. Recipe Data Structure

```csharp
public enum RecipeCategory {
    BuildingMaterials,
    Tools,
    Food,
    Processing,
    Furniture,
    Weapons,
    Clothing,
    Medicine,
    Components
}

public enum SkillType {
    Crafting,
    Masonry,
    Toolmaking,
    Cooking,
    Smelting,
    Carpentry,
    Tanning,
    Weaving,
    Construction,
    Alchemy
}

public enum ToolType {
    Saw,
    Hammer,
    Knife,
    Needle,
    MortarAndPestle,
    Chisel
}

public enum CraftingStation {
    Workbench,
    Campfire,
    Furnace,
    Smithy,
    Kiln,
    Oven,
    Tannery,
    BlastFurnace,
    AdvancedSmithy,
    Kitchen,
    Laboratory,
    Factory,
    SpinningWheel,
    Loom,
    ApothecaryTable
}

public class Recipe {
    public int Id { get; set; }
    public string Name { get; set; }
    public RecipeCategory Category { get; set; }
    public List<Ingredient> Ingredients { get; set; }
    public List<Product> Products { get; set; }
    public float ProductionTimeSeconds { get; set; }
    public SkillType RequiredSkill { get; set; }
    public int RequiredSkillLevel { get; set; }
    public ToolType? RequiredTool { get; set; }
    public CraftingStation? RequiredStation { get; set; }
    public int UnlockLevel { get; set; }
    public int ExperienceReward { get; set; }
    public string Description { get; set; }
}

public class Ingredient {
    public ItemType Item { get; set; }
    public int Quantity { get; set; }
    public bool Consumed { get; set; } = true;
}

public class Product {
    public ItemType Item { get; set; }
    public int Quantity { get; set; }
    public int QualityBonus { get; set; }
}
```

---

## 2. Recipe Categories

### Building Materials (25 recipes)

```
1. Wood Plank
   ID: 101
   Input: Wood ×2
   Output: Wood Plank ×5
   Time: 3s
   Skill: Crafting (0)
   Tool: Saw
   XP: 2
   Description: Process raw wood into usable planks

2. Stone Brick
   ID: 102
   Input: Stone ×2
   Output: Stone Brick ×1
   Time: 5s
   Skill: Masonry (0)
   Station: Workbench
   XP: 3
   Description: Cut stone into uniform bricks

3. Clay Brick
   ID: 103
   Input: Clay ×2
   Output: Clay Brick ×1
   Time: 8s
   Skill: Masonry (2)
   Station: Kiln
   XP: 5
   Description: Fire clay into durable bricks

4. Mortar
   ID: 104
   Input: Sand ×1 + Water ×1
   Output: Mortar ×2
   Time: 10s
   Skill: Masonry (1)
   XP: 3
   Description: Mix binding material for construction

5. Wooden Wall
   ID: 105
   Input: Wood Plank ×10 + Nails ×5
   Output: Wooden Wall ×1
   Time: 30s
   Skill: Construction (1)
   XP: 10
   Description: Basic wooden structural wall

6. Stone Wall
   ID: 106
   Input: Stone Brick ×15 + Mortar ×3
   Output: Stone Wall ×1
   Time: 60s
   Skill: Masonry (2)
   XP: 15
   Description: Durable stone wall

7. Wooden Floor
   ID: 107
   Input: Wood Plank ×8 + Nails ×4
   Output: Wooden Floor ×1
   Time: 25s
   Skill: Carpentry (1)
   XP: 8
   Description: Wooden flooring for structures

8. Stone Floor
   ID: 108
   Input: Stone Brick ×10 + Mortar ×2
   Output: Stone Floor ×1
   Time: 45s
   Skill: Masonry (2)
   XP: 12
   Description: Stone tile flooring

9. Wooden Roof
   ID: 109
   Input: Wood Plank ×12 + Thatch ×8
   Output: Wooden Roof ×1
   Time: 40s
   Skill: Construction (2)
   XP: 12
   Description: Basic thatched roof

10. Tile Roof
    ID: 110
    Input: Clay Tile ×20 + Mortar ×4
    Output: Tile Roof ×1
    Time: 90s
    Skill: Masonry (3)
    XP: 20
    Description: Weather-resistant tile roofing

11. Wooden Door
    ID: 111
    Input: Wood Plank ×8 + Iron Hinge ×2 + Nails ×4
    Output: Wooden Door ×1
    Time: 30s
    Skill: Carpentry (2)
    XP: 10
    Description: Basic wooden door

12. Reinforced Door
    ID: 112
    Input: Wood Plank ×6 + Iron Plate ×4 + Iron Hinge ×2
    Output: Reinforced Door ×1
    Time: 60s
    Skill: Construction (4)
    XP: 20
    Description: Sturdy reinforced door

13. Wooden Window
    ID: 113
    Input: Wood Plank ×6 + Glass ×2 + Nails ×3
    Output: Wooden Window ×1
    Time: 35s
    Skill: Carpentry (2)
    XP: 12
    Description: Window with glass pane

14. Foundation Stone
    ID: 114
    Input: Stone Brick ×20 + Mortar ×5
    Output: Foundation Stone ×1
    Time: 120s
    Skill: Masonry (3)
    XP: 25
    Description: Solid foundation block

15. Wooden Pillar
    ID: 115
    Input: Hardwood ×2 + Nails ×2
    Output: Wooden Pillar ×1
    Time: 20s
    Skill: Carpentry (1)
    XP: 6
    Description: Support pillar for structures

16. Stone Pillar
    ID: 116
    Input: Stone Brick ×8 + Mortar ×2
    Output: Stone Pillar ×1
    Time: 45s
    Skill: Masonry (2)
    XP: 12
    Description: Decorative stone column

17. Wooden Stairs
    ID: 117
    Input: Wood Plank ×10 + Nails ×4
    Output: Wooden Stairs ×1
    Time: 25s
    Skill: Carpentry (2)
    XP: 8
    Description: Staircase for elevation change

18. Stone Stairs
    ID: 118
    Input: Stone Brick ×12 + Mortar ×3
    Output: Stone Stairs ×1
    Time: 50s
    Skill: Masonry (3)
    XP: 15
    Description: Stone staircase

19. Clay Tile
    ID: 119
    Input: Clay ×1
    Output: Clay Tile ×3
    Time: 5s
    Skill: Masonry (1)
    Station: Kiln
    XP: 3
    Description: Roofing and flooring tiles

20. Thatch Bundle
    ID: 120
    Input: Straw ×5
    Output: Thatch Bundle ×1
    Time: 10s
    Skill: Crafting (0)
    XP: 2
    Description: Roofing material from dried vegetation

21. Iron Bar
    ID: 121
    Input: Iron Ingot ×2
    Output: Iron Bar ×4
    Time: 15s
    Skill: Smelting (3)
    Station: Smithy
    Tool: Hammer
    XP: 8
    Description: Long metal bars for construction

22. Iron Plate
    ID: 122
    Input: Iron Ingot ×1
    Output: Iron Plate ×2
    Time: 20s
    Skill: Smelting (3)
    Station: Smithy
    Tool: Hammer
    XP: 8
    Description: Flat metal sheets

23. Steel Beam
    ID: 123
    Input: Steel Ingot ×2
    Output: Steel Beam ×2
    Time: 30s
    Skill: Smelting (6)
    Station: Advanced Smithy
    Tool: Hammer
    XP: 15
    Description: Reinforced structural beam

24. Nails
    ID: 124
    Input: Iron Ingot ×1
    Output: Nails ×20
    Time: 10s
    Skill: Toolmaking (2)
    Station: Smithy
    Tool: Hammer
    XP: 5
    Description: Fasteners for construction

25. Iron Hinge
    ID: 125
    Input: Iron Ingot ×1
    Output: Iron Hinge ×2
    Time: 15s
    Skill: Toolmaking (3)
    Station: Smithy
    Tool: Hammer
    XP: 6
    Description: Door and chest hinges
```

### Tools (20 recipes)

```
1. Stone Axe
   ID: 201
   Input: Wood ×2 + Stone ×3
   Output: Stone Axe ×1
   Time: 15s
   Skill: Toolmaking (0)
   Station: Workbench
   XP: 5
   Description: Basic chopping tool (50 uses)

2. Stone Pickaxe
   ID: 202
   Input: Wood ×2 + Stone ×3
   Output: Stone Pickaxe ×1
   Time: 15s
   Skill: Toolmaking (0)
   Station: Workbench
   XP: 5
   Description: Basic mining tool (50 uses)

3. Stone Hoe
   ID: 203
   Input: Wood ×2 + Stone ×2
   Output: Stone Hoe ×1
   Time: 12s
   Skill: Toolmaking (0)
   Station: Workbench
   XP: 4
   Description: Basic farming tool (50 uses)

4. Stone Knife
   ID: 204
   Input: Wood ×1 + Stone ×1
   Output: Stone Knife ×1
   Time: 8s
   Skill: Toolmaking (0)
   Station: Workbench
   XP: 3
   Description: Basic cutting tool (50 uses)

5. Iron Axe
   ID: 205
   Input: Wood ×2 + Iron Ingot ×3
   Output: Iron Axe ×1
   Time: 30s
   Skill: Toolmaking (3)
   Station: Smithy
   Tool: Hammer
   XP: 15
   Description: Improved chopping tool (150 uses, 50% faster)

6. Iron Pickaxe
   ID: 206
   Input: Wood ×2 + Iron Ingot ×3
   Output: Iron Pickaxe ×1
   Time: 30s
   Skill: Toolmaking (3)
   Station: Smithy
   Tool: Hammer
   XP: 15
   Description: Improved mining tool (150 uses, 50% faster)

7. Iron Hoe
   ID: 207
   Input: Wood ×2 + Iron Ingot ×2
   Output: Iron Hoe ×1
   Time: 25s
   Skill: Toolmaking (3)
   Station: Smithy
   Tool: Hammer
   XP: 12
   Description: Improved farming tool (150 uses, 50% faster)

8. Iron Hammer
   ID: 208
   Input: Wood ×2 + Iron Ingot ×2
   Output: Iron Hammer ×1
   Time: 25s
   Skill: Toolmaking (3)
   Station: Smithy
   XP: 12
   Description: Crafting and construction tool (150 uses)

9. Steel Axe
   ID: 209
   Input: Hardwood ×2 + Steel Ingot ×3
   Output: Steel Axe ×1
   Time: 60s
   Skill: Toolmaking (6)
   Station: Advanced Smithy
   Tool: Hammer
   XP: 30
   Description: Professional chopping tool (500 uses, 100% faster)

10. Steel Pickaxe
    ID: 210
    Input: Hardwood ×2 + Steel Ingot ×3
    Output: Steel Pickaxe ×1
    Time: 60s
    Skill: Toolmaking (6)
    Station: Advanced Smithy
    Tool: Hammer
    XP: 30
    Description: Professional mining tool (500 uses, 100% faster)

11. Steel Hoe
    ID: 211
    Input: Hardwood ×2 + Steel Ingot ×2
    Output: Steel Hoe ×1
    Time: 50s
    Skill: Toolmaking (6)
    Station: Advanced Smithy
    Tool: Hammer
    XP: 25
    Description: Professional farming tool (500 uses, 100% faster)

12. Saw
    ID: 212
    Input: Wood ×1 + Iron Ingot ×2
    Output: Saw ×1
    Time: 30s
    Skill: Toolmaking (2)
    Station: Smithy
    Tool: Hammer
    XP: 12
    Description: Wood processing tool (150 uses)

13. Sickle
    ID: 213
    Input: Wood ×1 + Iron Ingot ×2
    Output: Sickle ×1
    Time: 25s
    Skill: Toolmaking (3)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Harvesting tool for crops (150 uses)

14. Shovel
    ID: 214
    Input: Wood ×2 + Iron Ingot ×2
    Output: Shovel ×1
    Time: 25s
    Skill: Toolmaking (2)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Digging and earthmoving tool (150 uses)

15. Chisel
    ID: 215
    Input: Iron Ingot ×1 + Wood ×1
    Output: Chisel ×1
    Time: 20s
    Skill: Toolmaking (4)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Stone carving tool (150 uses)

16. Shears
    ID: 216
    Input: Iron Ingot ×2
    Output: Shears ×1
    Time: 25s
    Skill: Toolmaking (3)
    Station: Smithy
    Tool: Hammer
    XP: 12
    Description: Wool harvesting tool (150 uses)

17. Needle
    ID: 217
    Input: Iron Ingot ×1
    Output: Needle ×3
    Time: 15s
    Skill: Toolmaking (2)
    Station: Smithy
    Tool: Hammer
    XP: 6
    Description: Sewing and crafting tool (150 uses)

18. Fishing Rod
    ID: 218
    Input: Wood ×2 + String ×3
    Output: Fishing Rod ×1
    Time: 20s
    Skill: Crafting (1)
    Station: Workbench
    XP: 8
    Description: Tool for catching fish (100 uses)

19. Bucket
    ID: 219
    Input: Iron Ingot ×3
    Output: Bucket ×1
    Time: 25s
    Skill: Toolmaking (2)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Liquid container and tool (unlimited uses)

20. Mortar and Pestle
    ID: 220
    Input: Stone ×2
    Output: Mortar and Pestle ×1
    Time: 15s
    Skill: Crafting (0)
    Station: Workbench
    XP: 5
    Description: Grinding tool for processing (unlimited uses)
```

### Food & Cooking (20 recipes)

```
1. Bread
   ID: 301
   Input: Wheat ×2 + Water ×1
   Output: Bread ×2
   Time: 20s
   Skill: Cooking (0)
   Station: Oven
   XP: 5
   Description: Basic sustenance

2. Cooked Meat
   ID: 302
   Input: Raw Meat ×1 + Fire/Fuel
   Output: Cooked Meat ×1
   Time: 15s
   Skill: Cooking (0)
   Station: Campfire or Oven
   XP: 4
   Description: Simple cooked protein

3. Vegetable Stew
   ID: 303
   Input: Potato ×2 + Carrot ×2 + Water ×1
   Output: Vegetable Stew ×4
   Time: 45s
   Skill: Cooking (2)
   Station: Cooking Pot
   XP: 10
   Description: Nutritious vegetable dish

4. Meat Stew
   ID: 304
   Input: Cooked Meat ×2 + Potato ×2 + Water ×1
   Output: Meat Stew ×4
   Time: 60s
   Skill: Cooking (3)
   Station: Cooking Pot
   XP: 12
   Description: Hearty meat dish

5. Advanced Meal
   ID: 305
   Input: Cooked Meat ×2 + Vegetables ×3 + Bread ×2 + Spices ×1
   Output: Advanced Meal ×2
   Time: 90s
   Skill: Cooking (5)
   Station: Kitchen
   XP: 25
   Description: High-quality nutritious meal

6. Baked Potato
   ID: 306
   Input: Potato ×1
   Output: Baked Potato ×1
   Time: 20s
   Skill: Cooking (0)
   Station: Campfire or Oven
   XP: 3
   Description: Simple cooked vegetable

7. Roasted Vegetables
   ID: 307
   Input: Mixed Vegetables ×3 + Oil ×1
   Output: Roasted Vegetables ×2
   Time: 30s
   Skill: Cooking (2)
   Station: Oven
   XP: 8
   Description: Flavorful vegetable dish

8. Preserved Meat
   ID: 308
   Input: Raw Meat ×2 + Salt ×1
   Output: Preserved Meat ×2
   Time: 40s
   Skill: Cooking (3)
   Station: Workbench
   XP: 10
   Description: Long-lasting meat (lasts 10× longer)

9. Dried Fruit
   ID: 309
   Input: Fresh Fruit ×3
   Output: Dried Fruit ×2
   Time: 30s
   Skill: Cooking (2)
   Station: Oven (low heat)
   XP: 6
   Description: Preserved fruit snack

10. Cheese
    ID: 310
    Input: Milk ×5 + Rennet ×1
    Output: Cheese ×1
    Time: 120s
    Skill: Cooking (4)
    Station: Kitchen
    XP: 15
    Description: Dairy product with long shelf life

11. Butter
    ID: 311
    Input: Milk ×3 + Salt ×1
    Output: Butter ×1
    Time: 60s
    Skill: Cooking (3)
    Station: Kitchen
    XP: 10
    Description: Cooking ingredient and spread

12. Flour
    ID: 312
    Input: Wheat ×2
    Output: Flour ×2
    Time: 10s
    Skill: Cooking (0)
    Station: Workbench
    Tool: Mortar and Pestle
    XP: 3
    Description: Ground grain for baking

13. Pasta
    ID: 313
    Input: Flour ×2 + Egg ×1 + Water ×1
    Output: Pasta ×2
    Time: 25s
    Skill: Cooking (3)
    Station: Kitchen
    XP: 8
    Description: Dough product for cooking

14. Pasta Dish
    ID: 314
    Input: Pasta ×2 + Tomato ×2 + Cooked Meat ×1
    Output: Pasta Dish ×2
    Time: 45s
    Skill: Cooking (4)
    Station: Kitchen
    XP: 12
    Description: Filling pasta meal

15. Soup
    ID: 315
    Input: Vegetables ×3 + Water ×2 + Salt ×1
    Output: Soup ×4
    Time: 40s
    Skill: Cooking (1)
    Station: Cooking Pot
    XP: 8
    Description: Warm vegetable soup

16. Fish Fillet
    ID: 316
    Input: Raw Fish ×1
    Output: Fish Fillet ×1
    Time: 15s
    Skill: Cooking (1)
    Station: Campfire or Oven
    XP: 4
    Description: Simple cooked fish

17. Fish Stew
    ID: 317
    Input: Fish Fillet ×2 + Potato ×2 + Water ×1 + Herbs ×1
    Output: Fish Stew ×3
    Time: 50s
    Skill: Cooking (3)
    Station: Cooking Pot
    XP: 10
    Description: Seafood dish

18. Pie
    ID: 318
    Input: Flour ×3 + Fat ×1 + Fruit ×3 + Sugar ×1
    Output: Pie ×1
    Time: 60s
    Skill: Cooking (4)
    Station: Oven
    XP: 15
    Description: Baked dessert

19. Pickled Vegetables
    ID: 319
    Input: Vegetables ×4 + Vinegar ×1 + Salt ×1
    Output: Pickled Vegetables ×4
    Time: 30s
    Skill: Cooking (3)
    Station: Workbench
    XP: 8
    Description: Long-lasting preserved vegetables

20. Herbal Tea
    ID: 320
    Input: Herbs ×2 + Water ×1
    Output: Herbal Tea ×1
    Time: 15s
    Skill: Cooking (2)
    Station: Campfire or Oven
    XP: 5
    Description: Soothing beverage with minor healing
```

### Processing (20 recipes)

```
1. Iron Ingot
   ID: 401
   Input: Iron Ore ×2 + Coal ×1
   Output: Iron Ingot ×1
   Time: 30s
   Skill: Smelting (2)
   Station: Furnace
   XP: 8
   Description: Refined iron for crafting

2. Steel Ingot
   ID: 402
   Input: Iron Ingot ×2 + Carbon ×1
   Output: Steel Ingot ×1
   Time: 60s
   Skill: Smelting (5)
   Station: Blast Furnace
   XP: 20
   Description: High-quality metal alloy

3. Charcoal
   ID: 403
   Input: Wood ×5
   Output: Charcoal ×10
   Time: 60s
   Skill: None
   Station: Kiln
   XP: 3
   Description: Purified fuel from wood

4. Glass
   ID: 404
   Input: Sand ×4 + Coal ×1
   Output: Glass ×2
   Time: 30s
   Skill: Smelting (3)
   Station: Furnace
   XP: 10
   Description: Transparent material for windows

5. Leather
   ID: 405
   Input: Raw Hide ×1 + Tanning Agent ×1
   Output: Leather ×1
   Time: 120s
   Skill: Tanning (2)
   Station: Tannery
   XP: 15
   Description: Processed hide for crafting

6. Thread
   ID: 406
   Input: Wool ×2 or Cotton ×2
   Output: Thread ×4
   Time: 10s
   Skill: Weaving (0)
   Station: Spinning Wheel
   XP: 4
   Description: Basic textile material

7. Cloth
   ID: 407
   Input: Thread ×8
   Output: Cloth ×1
   Time: 30s
   Skill: Weaving (1)
   Station: Loom
   XP: 10
   Description: Woven fabric material

8. Rope
   ID: 408
   Input: Fiber ×6
   Output: Rope ×2
   Time: 15s
   Skill: Crafting (0)
   Station: Workbench
   XP: 4
   Description: Strong binding material

9. Paper
   ID: 409
   Input: Wood Pulp ×3 + Water ×1
   Output: Paper ×4
   Time: 20s
   Skill: Crafting (2)
   Station: Workbench
   XP: 6
   Description: Writing and packaging material

10. Oil
    ID: 410
    Input: Olives ×5 or Seeds ×3
    Output: Oil ×1
    Time: 30s
    Skill: Cooking (2)
    Station: Workbench
    Tool: Mortar and Pestle
    XP: 6
    Description: Cooking and crafting ingredient

11. Salt
    ID: 411
    Input: Salt Water ×5
    Output: Salt ×2
    Time: 60s
    Skill: None
    Station: Campfire or Oven
    XP: 3
    Description: Preserving and seasoning mineral

12. Sugar
    ID: 412
    Input: Sugar Cane ×3
    Output: Sugar ×2
    Time: 15s
    Skill: Cooking (1)
    Station: Workbench
    Tool: Mortar and Pestle
    XP: 4
    Description: Sweetening ingredient

13. Dye (Red)
    ID: 413
    Input: Red Flowers ×4 + Water ×1
    Output: Red Dye ×2
    Time: 20s
    Skill: Crafting (2)
    Station: Workbench
    XP: 5
    Description: Coloring agent

14. Dye (Blue)
    ID: 414
    Input: Blue Flowers ×4 + Water ×1
    Output: Blue Dye ×2
    Time: 20s
    Skill: Crafting (2)
    Station: Workbench
    XP: 5
    Description: Coloring agent

15. Dye (Yellow)
    ID: 415
    Input: Yellow Flowers ×4 + Water ×1
    Output: Yellow Dye ×2
    Time: 20s
    Skill: Crafting (2)
    Station: Workbench
    XP: 5
    Description: Coloring agent

16. Treated Wood
    ID: 416
    Input: Wood Plank ×4 + Oil ×1
    Output: Treated Wood ×4
    Time: 30s
    Skill: Carpentry (3)
    Station: Workbench
    XP: 8
    Description: Weather-resistant wood

17. Metal Chain
    ID: 417
    Input: Iron Bar ×2
    Output: Metal Chain ×3
    Time: 25s
    Skill: Toolmaking (4)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Linked metal for various uses

18. Carbon
    ID: 418
    Input: Charcoal ×2
    Output: Carbon ×1
    Time: 20s
    Skill: Smelting (4)
    Station: Furnace
    XP: 8
    Description: Steel-making component

19. Tanning Agent
    ID: 419
    Input: Bark ×5 + Water ×2
    Output: Tanning Agent ×3
    Time: 30s
    Skill: Tanning (1)
    Station: Workbench
    XP: 6
    Description: Required for leather production

20. Wood Pulp
    ID: 420
    Input: Wood ×2 + Water ×3
    Output: Wood Pulp ×4
    Time: 15s
    Skill: Crafting (1)
    Station: Workbench
    XP: 4
    Description: Base material for paper
```

### Furniture & Decoration (15 recipes)

```
1. Wooden Chair
   ID: 501
   Input: Wood Plank ×5 + Nails ×2
   Output: Wooden Chair ×1
   Time: 20s
   Skill: Carpentry (1)
   Station: Workbench
   XP: 8
   Description: Basic seating furniture

2. Wooden Table
   ID: 502
   Input: Wood Plank ×10 + Nails ×4
   Output: Wooden Table ×1
   Time: 30s
   Skill: Carpentry (2)
   Station: Workbench
   XP: 12
   Description: Work and dining surface

3. Bed
   ID: 503
   Input: Wood Plank ×15 + Cloth ×4 + Nails ×6
   Output: Bed ×1
   Time: 60s
   Skill: Carpentry (3)
   Station: Workbench
   XP: 20
   Description: Resting furniture

4. Chest
   ID: 504
   Input: Wood Plank ×8 + Iron Hinge ×2
   Output: Chest ×1
   Time: 25s
   Skill: Carpentry (2)
   Station: Workbench
   XP: 10
   Description: Storage container (32 slots)

5. Bookshelf
   ID: 505
   Input: Wood Plank ×12 + Nails ×4
   Output: Bookshelf ×1
   Time: 40s
   Skill: Carpentry (4)
   Station: Workbench
   XP: 15
   Description: Storage for books and items

6. Wardrobe
   ID: 506
   Input: Wood Plank ×15 + Cloth ×2 + Iron Hinge ×2
   Output: Wardrobe ×1
   Time: 50s
   Skill: Carpentry (3)
   Station: Workbench
   XP: 15
   Description: Clothing storage furniture

7. Display Case
   ID: 507
   Input: Wood Plank ×6 + Glass ×4 + Nails ×2
   Output: Display Case ×1
   Time: 35s
   Skill: Carpentry (4)
   Station: Workbench
   XP: 12
   Description: Showcase for valuable items

8. Painting Frame
   ID: 508
   Input: Wood Plank ×4 + Cloth ×1
   Output: Painting Frame ×1
   Time: 20s
   Skill: Crafting (2)
   Station: Workbench
   XP: 6
   Description: Decorative wall hanging

9. Rug
   ID: 509
   Input: Cloth ×6 + Dye ×1
   Output: Rug ×1
   Time: 40s
   Skill: Weaving (2)
   Station: Loom
   XP: 12
   Description: Floor covering decoration

10. Curtains
    ID: 510
    Input: Cloth ×4 + Thread ×2
    Output: Curtains ×1
    Time: 25s
    Skill: Weaving (2)
    Station: Loom
    XP: 8
    Description: Window decoration

11. Flower Pot
    ID: 511
    Input: Clay ×2
    Output: Flower Pot ×1
    Time: 15s
    Skill: Crafting (1)
    Station: Kiln
    XP: 5
    Description: Container for plants

12. Candle
    ID: 512
    Input: Wax ×2 + String ×1
    Output: Candle ×3
    Time: 15s
    Skill: Crafting (1)
    Station: Workbench
    XP: 4
    Description: Light source and decoration

13. Lantern
    ID: 513
    Input: Iron Plate ×2 + Glass ×1 + Candle ×1
    Output: Lantern ×1
    Time: 30s
    Skill: Toolmaking (3)
    Station: Smithy
    Tool: Hammer
    XP: 10
    Description: Portable light source

14. Bench
    ID: 514
    Input: Wood Plank ×8 + Nails ×3
    Output: Bench ×1
    Time: 25s
    Skill: Carpentry (2)
    Station: Workbench
    XP: 10
    Description: Outdoor seating

15. Sign
    ID: 515
    Input: Wood Plank ×3 + Nails ×1
    Output: Sign ×1
    Time: 10s
    Skill: Crafting (0)
    Station: Workbench
    XP: 3
    Description: Customizable message board
```

---

## 3. Crafting Stations

### Station Progression

```
Tier 1 - Basic:
  - Workbench: Simple crafting, tools, basic building
    * Unlocked: Level 0
    * Recipes: 35 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: None
    
  - Campfire: Basic cooking, warmth
    * Unlocked: Level 0
    * Recipes: 15 recipes
    * Speed: 0.8× (slower than proper stations)
    * Worker Slots: 1
    * Fuel: Wood (consumed every 10 crafts)

Tier 2 - Intermediate:
  - Furnace: Smelting ores
    * Unlocked: Level 2 (Smelting)
    * Recipes: 8 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: Coal or Charcoal (1 per operation)
    
  - Smithy: Metal tools
    * Unlocked: Level 2 (Toolmaking)
    * Recipes: 20 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: Coal or Charcoal (1 per 5 operations)
    
  - Kiln: Ceramics, charcoal
    * Unlocked: Level 1 (Masonry)
    * Recipes: 6 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: Wood (5 per operation for charcoal)
    
  - Oven: Baking, advanced cooking
    * Unlocked: Level 1 (Cooking)
    * Recipes: 12 recipes
    * Speed: 1.2×
    * Worker Slots: 1
    * Fuel: Wood or Charcoal (1 per 3 operations)
    
  - Tannery: Leather processing
    * Unlocked: Level 2 (Tanning)
    * Recipes: 2 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: None (uses chemicals)
    
  - Cooking Pot: Stews and soups
    * Unlocked: Level 1 (Cooking)
    * Recipes: 8 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: Fire (campfire or stove)

Tier 3 - Advanced:
  - Blast Furnace: Steel production
    * Unlocked: Level 5 (Smelting)
    * Recipes: 4 recipes
    * Speed: 1.5×
    * Worker Slots: 2
    * Fuel: Coal (2 per operation)
    
  - Advanced Smithy: Steel tools, complex metalwork
    * Unlocked: Level 5 (Toolmaking)
    * Recipes: 12 recipes
    * Speed: 1.3×
    * Worker Slots: 2
    * Fuel: Coal (1 per 3 operations)
    
  - Kitchen: Advanced cooking, meals
    * Unlocked: Level 4 (Cooking)
    * Recipes: 15 recipes
    * Speed: 1.4×
    * Worker Slots: 2
    * Fuel: Wood or Charcoal (1 per 5 operations)
    
  - Laboratory: Chemical processing
    * Unlocked: Level 6 (Alchemy)
    * Recipes: 10 recipes
    * Speed: 1.2×
    * Worker Slots: 1
    * Fuel: None
    
  - Factory: Automated production
    * Unlocked: Level 7 (Crafting)
    * Recipes: 20 recipes
    * Speed: 2.0×
    * Worker Slots: 4
    * Fuel: Coal (3 per operation)
    
  - Loom: Advanced textiles
    * Unlocked: Level 3 (Weaving)
    * Recipes: 8 recipes
    * Speed: 1.3×
    * Worker Slots: 1
    * Fuel: None
    
  - Apothecary Table: Medicine crafting
    * Unlocked: Level 5 (Alchemy)
    * Recipes: 12 recipes
    * Speed: 1.0×
    * Worker Slots: 1
    * Fuel: None
```

### Station Requirements (C# Class)

```csharp
public class CraftingStation {
    public StationType Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<Recipe> AvailableRecipes { get; set; }
    public float SpeedMultiplier { get; set; }
    public int WorkerSlots { get; set; }
    public Dictionary<ItemType, int> FuelRequirements { get; set; }
    public int FuelConsumptionRate { get; set; } // Operations per fuel unit
    public SkillType RequiredSkill { get; set; }
    public int RequiredSkillLevel { get; set; }
    public List<Ingredient> ConstructionCost { get; set; }
    public float ConstructionTime { get; set; }
}

// Example: Furnace
var furnace = new CraftingStation {
    Type = StationType.Furnace,
    Name = "Furnace",
    Description = "Basic smelting furnace for ore processing",
    SpeedMultiplier = 1.0f,
    WorkerSlots = 1,
    FuelRequirements = new Dictionary<ItemType, int> { 
        { ItemType.Coal, 1 },
        { ItemType.Charcoal, 1 }
    },
    FuelConsumptionRate = 1, // 1 fuel per operation
    RequiredSkill = SkillType.Smelting,
    RequiredSkillLevel = 2,
    ConstructionCost = new List<Ingredient> {
        new Ingredient { Item = ItemType.StoneBrick, Quantity = 20 },
        new Ingredient { Item = ItemType.IronBar, Quantity = 4 }
    },
    ConstructionTime = 300f // 5 minutes
};

// Example: Advanced Smithy
var advancedSmithy = new CraftingStation {
    Type = StationType.AdvancedSmithy,
    Name = "Advanced Smithy",
    Description = "Professional metalworking station with improved tools",
    SpeedMultiplier = 1.3f,
    WorkerSlots = 2,
    FuelRequirements = new Dictionary<ItemType, int> { 
        { ItemType.Coal, 1 }
    },
    FuelConsumptionRate = 3, // 1 fuel per 3 operations
    RequiredSkill = SkillType.Toolmaking,
    RequiredSkillLevel = 5,
    ConstructionCost = new List<Ingredient> {
        new Ingredient { Item = ItemType.StoneBrick, Quantity = 30 },
        new Ingredient { Item = ItemType.SteelIngot, Quantity = 8 },
        new Ingredient { Item = ItemType.IronBar, Quantity = 10 }
    },
    ConstructionTime = 1800f // 30 minutes
};
```

---

## 4. Skill Effects on Crafting

### Production Time Reduction

```
Skill Level: Time Bonus (Multiplier)
Level 0: 100% (1.00×) - Base time
Level 1: 95% (0.95×) - 5% faster
Level 2: 90% (0.90×) - 10% faster
Level 3: 85% (0.85×) - 15% faster
Level 4: 80% (0.80×) - 20% faster
Level 5: 75% (0.75×) - 25% faster
Level 6: 70% (0.70×) - 30% faster
Level 7: 65% (0.65×) - 35% faster
Level 8: 60% (0.60×) - 40% faster
Level 9: 55% (0.55×) - 45% faster
Level 10: 50% (0.50×) - 50% faster (half time)

Formula: Time = BaseTime × (0.95^SkillLevel)
```

### Quality Bonus

```
Skill Level: Quality Bonus
Level 0-2: 0 bonus (Poor/Normal only)
  - Poor: 0-25% (30% chance)
  - Normal: 26-50% (70% chance)
  
Level 3-4: +10% chance of Good
  - Poor: 0-25% (20% chance)
  - Normal: 26-50% (60% chance)
  - Good: 51-75% (20% chance)
  
Level 5-6: +20% chance of Good, +5% Excellent
  - Poor: 0-25% (15% chance)
  - Normal: 26-50% (50% chance)
  - Good: 51-75% (30% chance)
  - Excellent: 76-95% (5% chance)
  
Level 7-8: +30% chance of Good, +15% Excellent
  - Poor: 0-25% (10% chance)
  - Normal: 26-50% (35% chance)
  - Good: 51-75% (40% chance)
  - Excellent: 76-95% (15% chance)
  
Level 9-10: +40% chance of Good, +25% Excellent, +5% Masterwork
  - Poor: 0-25% (5% chance)
  - Normal: 26-50% (25% chance)
  - Good: 51-75% (45% chance)
  - Excellent: 76-95% (20% chance)
  - Masterwork: 96-100% (5% chance)
```

### Quality Multipliers

```csharp
public static float GetQualityMultiplier(ItemQuality quality) {
    switch (quality) {
        case ItemQuality.Poor: return 0.70f;       // 70% effectiveness
        case ItemQuality.Normal: return 1.00f;     // 100% effectiveness (baseline)
        case ItemQuality.Good: return 1.15f;       // 115% effectiveness
        case ItemQuality.Excellent: return 1.30f;  // 130% effectiveness
        case ItemQuality.Masterwork: return 1.50f; // 150% effectiveness
        default: return 1.00f;
    }
}

// Quality affects:
// - Tool durability (higher quality = more uses)
// - Weapon damage
// - Armor protection
// - Food nutrition value
// - Building durability
// - Sell price
```

---

## 5. Recipe Unlocking

### Unlock Methods

```
1. Automatic (Level 0)
   - Available from character creation
   - Basic survival recipes
   - Stone tools, simple food
   - Count: 30 recipes

2. Skill Level Unlock
   - Unlock at specific skill threshold
   - Most common method (60 recipes)
   - Examples:
     * Masonry 2 → Clay Brick
     * Toolmaking 3 → Iron Axe
     * Cooking 5 → Advanced Meal

3. Discovery
   - Find recipe book/scroll in world
   - Rare recipes (5 recipes)
   - Examples:
     * Ancient Forge techniques
     * Secret alchemy recipes
     * Masterwork crafting methods

4. Research
   - Spend resources at research station
   - Technology advancement (3 recipes)
   - Cost: 500-2000 credits + materials
   - Examples:
     * Advanced alloys
     * Automation blueprints

5. Teaching
   - Learn from another player or AI agent
   - Social learning method
   - Requires: Teacher skill level +5 above recipe
   - Time: 5-15 minutes
   - Cost: 50-200 credits (optional)

6. Blueprint Purchase
   - Buy from NPC vendor
   - Specialized recipes (2 recipes)
   - Cost: 100-500 credits
   - Examples:
     * Rare furniture designs
     * Advanced machinery
```

### Unlock Progression

```
Tier 1 (Skill 0-2): 30 recipes
  Categories:
    - Building: Wood Plank, Stone Brick, Mortar, Basic walls
    - Tools: All stone tools, basic iron tools
    - Food: Bread, Cooked Meat, basic stews
    - Processing: Charcoal, basic smelting
    - Furniture: Chair, Table, Chest

Tier 2 (Skill 3-5): 35 recipes
  Categories:
    - Building: Clay Brick, Tile Roof, Reinforced Door, Stone Pillar
    - Tools: All iron tools, Steel Axe/Pickaxe (level 5)
    - Food: Advanced meals, cheese, pasta, pie
    - Processing: Steel Ingot (level 5), Glass, Leather
    - Furniture: Bed, Wardrobe, Bookshelf, Display Case

Tier 3 (Skill 6-8): 25 recipes
  Categories:
    - Building: Steel Beam, Advanced structures
    - Tools: All steel tools, Masterwork upgrades
    - Food: Master chef recipes
    - Processing: Advanced alloys, Chemical processing
    - Furniture: Masterwork furniture
    - Special: Factory components, Automation

Tier 4 (Skill 9-10): 10 recipes
  Categories:
    - Building: Masterwork architectural elements
    - Tools: Legendary tool recipes
    - Food: Epicurean masterpieces
    - Processing: Experimental materials
    - Special: Meteor defense technology
```

---

## 6. Bulk Crafting

### Batch Production System

```csharp
public class BatchCrafting {
    public int MaxQueueSize { get; set; } = 50; // Maximum items in queue
    public int BatchSize { get; set; } // 1, 5, 10, or Max
    
    // Calculate batch time with efficiency bonus
    public float CalculateBatchTime(Recipe recipe, int quantity) {
        float baseTime = recipe.ProductionTimeSeconds * quantity;
        float efficiencyBonus = GetBatchEfficiencyBonus(quantity);
        return baseTime * efficiencyBonus;
    }
    
    public float GetBatchEfficiencyBonus(int quantity) {
        if (quantity == 1) return 1.0f;      // 100%
        if (quantity <= 5) return 0.95f;     // 5% faster per item
        if (quantity <= 10) return 0.90f;    // 10% faster per item
        return 0.85f;                        // 15% faster per item (max)
    }
}

// Example usage:
// Crafting 10 Iron Axes
// Base time: 30s × 10 = 300s (5 minutes)
// With batch bonus (10 items): 300s × 0.90 = 270s (4.5 minutes)
// Time saved: 30 seconds
```

### Batch Size Options

```
UI Options for Batch Crafting:

1× (Single Item):
  - Time: 100% of base
  - Materials: Exact recipe cost
  - Use case: Testing new recipe, limited materials

5× (Small Batch):
  - Time: 95% per item (5% faster)
  - Materials: 5× recipe cost
  - Time saved: 2.5% total
  - Use case: Regular production

10× (Large Batch):
  - Time: 90% per item (10% faster)
  - Materials: 10× recipe cost
  - Time saved: 10% total
  - Use case: Mass production

Max (Material Limited):
  - Time: 85% per item (15% faster)
  - Materials: All available in inventory
  - Time saved: Up to 15% total
  - Use case: Full inventory utilization
```

### Cancellation Mechanics

```
Cancel Crafting:
  - Can cancel at any time during production
  - Partial completion returns:
    * Completed items: Full quantity crafted so far
    * Unprocessed materials: 100% returned
    * In-progress materials: 50% returned (waste)
  - XP gain: Proportional to completion percentage
  
Example:
  Crafting 10 Iron Axes (300s total)
  Cancel at 150s (50% complete):
    - Receive: 5 completed Iron Axes
    - Return: 50% of remaining materials
    - XP: 50% of full reward (7 XP instead of 15)
```

---

## 7. Recipe Database Format

### JSON Schema

```json
{
  "version": "1.0",
  "totalRecipes": 100,
  "lastUpdated": "2026-02-01",
  "recipes": [
    {
      "id": 101,
      "name": "Wood Plank",
      "category": "BuildingMaterials",
      "ingredients": [
        {
          "item": "Wood",
          "quantity": 2,
          "consumed": true
        }
      ],
      "products": [
        {
          "item": "WoodPlank",
          "quantity": 5,
          "qualityBonus": 0
        }
      ],
      "productionTime": 3.0,
      "requiredSkill": "Crafting",
      "requiredSkillLevel": 0,
      "requiredTool": "Saw",
      "requiredStation": null,
      "unlockMethod": "Automatic",
      "unlockRequirement": null,
      "experienceReward": 2,
      "description": "Process raw wood into usable planks",
      "stackSize": 100
    },
    {
      "id": 205,
      "name": "Iron Axe",
      "category": "Tools",
      "ingredients": [
        {
          "item": "Wood",
          "quantity": 2,
          "consumed": true
        },
        {
          "item": "IronIngot",
          "quantity": 3,
          "consumed": true
        }
      ],
      "products": [
        {
          "item": "IronAxe",
          "quantity": 1,
          "qualityBonus": 0
        }
      ],
      "productionTime": 30.0,
      "requiredSkill": "Toolmaking",
      "requiredSkillLevel": 3,
      "requiredTool": "Hammer",
      "requiredStation": "Smithy",
      "unlockMethod": "SkillLevel",
      "unlockRequirement": "Toolmaking 3",
      "experienceReward": 15,
      "description": "Improved chopping tool (150 uses, 50% faster)",
      "durability": 150,
      "efficiency": 1.5,
      "stackSize": 1
    },
    {
      "id": 305,
      "name": "Advanced Meal",
      "category": "Food",
      "ingredients": [
        {
          "item": "CookedMeat",
          "quantity": 2,
          "consumed": true
        },
        {
          "item": "Vegetables",
          "quantity": 3,
          "consumed": true
        },
        {
          "item": "Bread",
          "quantity": 2,
          "consumed": true
        },
        {
          "item": "Spices",
          "quantity": 1,
          "consumed": true
        }
      ],
      "products": [
        {
          "item": "AdvancedMeal",
          "quantity": 2,
          "qualityBonus": 5
        }
      ],
      "productionTime": 90.0,
      "requiredSkill": "Cooking",
      "requiredSkillLevel": 5,
      "requiredTool": null,
      "requiredStation": "Kitchen",
      "unlockMethod": "SkillLevel",
      "unlockRequirement": "Cooking 5",
      "experienceReward": 25,
      "description": "High-quality nutritious meal",
      "nutritionValue": 80,
      "stackSize": 20
    }
  ],
  
  "categories": {
    "BuildingMaterials": {
      "recipeCount": 25,
      "primarySkill": "Masonry",
      "description": "Structures and construction materials"
    },
    "Tools": {
      "recipeCount": 20,
      "primarySkill": "Toolmaking",
      "description": "Equipment for gathering and crafting"
    },
    "Food": {
      "recipeCount": 20,
      "primarySkill": "Cooking",
      "description": "Consumable items for survival"
    },
    "Processing": {
      "recipeCount": 20,
      "primarySkill": "Smelting",
      "description": "Resource refinement and materials"
    },
    "Furniture": {
      "recipeCount": 15,
      "primarySkill": "Carpentry",
      "description": "Decorative and functional items"
    }
  }
}
```

### Database Implementation (C#)

```csharp
public class RecipeDatabase {
    private Dictionary<int, Recipe> _recipes;
    private Dictionary<RecipeCategory, List<Recipe>> _recipesByCategory;
    private Dictionary<SkillType, List<Recipe>> _recipesBySkill;
    
    public RecipeDatabase() {
        _recipes = new Dictionary<int, Recipe>();
        _recipesByCategory = new Dictionary<RecipeCategory, List<Recipe>>();
        _recipesBySkill = new Dictionary<SkillType, List<Recipe>>();
        LoadRecipes();
    }
    
    public Recipe GetRecipe(int id) {
        return _recipes.TryGetValue(id, out var recipe) ? recipe : null;
    }
    
    public List<Recipe> GetRecipesByCategory(RecipeCategory category) {
        return _recipesByCategory.TryGetValue(category, out var recipes) 
            ? recipes : new List<Recipe>();
    }
    
    public List<Recipe> GetRecipesBySkill(SkillType skill) {
        return _recipesBySkill.TryGetValue(skill, out var recipes) 
            ? recipes : new List<Recipe>();
    }
    
    public List<Recipe> GetAvailableRecipes(int skillLevel) {
        return _recipes.Values
            .Where(r => r.UnlockLevel <= skillLevel)
            .OrderBy(r => r.RequiredSkillLevel)
            .ToList();
    }
    
    public bool CanCraft(Recipe recipe, Inventory inventory, int skillLevel) {
        // Check skill requirement
        if (skillLevel < recipe.RequiredSkillLevel)
            return false;
        
        // Check ingredients
        foreach (var ingredient in recipe.Ingredients) {
            if (inventory.GetItemCount(ingredient.Item) < ingredient.Quantity)
                return false;
        }
        
        return true;
    }
    
    private void LoadRecipes() {
        // Load from JSON file
        var json = File.ReadAllText("Data/recipes.json");
        var data = JsonSerializer.Deserialize<RecipeData>(json);
        
        foreach (var recipe in data.Recipes) {
            _recipes[recipe.Id] = recipe;
            
            // Index by category
            if (!_recipesByCategory.ContainsKey(recipe.Category))
                _recipesByCategory[recipe.Category] = new List<Recipe>();
            _recipesByCategory[recipe.Category].Add(recipe);
            
            // Index by skill
            if (!_recipesBySkill.ContainsKey(recipe.RequiredSkill))
                _recipesBySkill[recipe.RequiredSkill] = new List<Recipe>();
            _recipesBySkill[recipe.RequiredSkill].Add(recipe);
        }
    }
}
```

---

## 8. Tool Durability & Repair

### Durability Constants (from technical-constants.md)

```csharp
public static class ToolConstants {
    // Durability (uses before breaking)
    public const int DURABILITY_STONE = 50;
    public const int DURABILITY_IRON = 150;
    public const int DURABILITY_STEEL = 500;
    
    // Repair mechanics
    public const float REPAIR_COST_PERCENT = 50.0f;      // 50% of original material cost
    public const int REPAIR_DURABILITY_RESTORED = 80;    // Repair restores 80% of max durability
    
    // Efficiency multipliers
    public const float EFFICIENCY_STONE = 1.0f;          // Baseline
    public const float EFFICIENCY_IRON = 1.5f;           // 50% faster
    public const float EFFICIENCY_STEEL = 2.0f;          // 100% faster
}

// Tool class implementation
public class Tool : Item {
    public int MaxDurability { get; set; }
    public int CurrentDurability { get; set; }
    public float EfficiencyMultiplier { get; set; }
    public ToolType Type { get; set; }
    public ToolMaterial Material { get; set; }
    
    public bool IsBroken => CurrentDurability <= 0;
    
    public void Use() {
        CurrentDurability--;
        if (CurrentDurability <= 0) {
            CurrentDurability = 0;
            // Tool breaks - notify player
        }
    }
    
    public bool CanRepair() => CurrentDurability < MaxDurability;
    
    public int CalculateRepairCost() {
        // 50% of original material cost based on durability lost
        float durabilityLost = MaxDurability - CurrentDurability;
        float percentLost = durabilityLost / MaxDurability;
        return (int)(percentLost * ToolConstants.REPAIR_COST_PERCENT);
    }
}
```

### Tool Material Tiers

```
Stone Tools (Tier 1):
  - Durability: 50 uses
  - Efficiency: 1.0× (baseline)
  - Unlock: Level 0
  - Repair: Cannot be repaired (too primitive)
  - Best for: Early game, abundant materials

Iron Tools (Tier 2):
  - Durability: 150 uses (3× stone)
  - Efficiency: 1.5× (50% faster)
  - Unlock: Level 3 (Toolmaking)
  - Repair: 50% material cost, restores 80% durability
  - Best for: Mid-game efficiency

Steel Tools (Tier 3):
  - Durability: 500 uses (10× stone)
  - Efficiency: 2.0× (100% faster)
  - Unlock: Level 6 (Toolmaking)
  - Repair: 50% material cost, restores 80% durability
  - Best for: Late-game mass production

Masterwork Tools (Tier 4):
  - Durability: 750 uses (15× stone)
  - Efficiency: 2.5× (150% faster)
  - Unlock: Discovery/Research only
  - Repair: 50% material cost, restores 80% durability
  - Best for: Ultimate efficiency
```

---

## 9. Integration with Other Systems

### Economy Integration

```csharp
public class RecipeEconomy {
    // Calculate item value based on recipe
    public float CalculateItemValue(Recipe recipe) {
        float materialCost = 0;
        foreach (var ingredient in recipe.Ingredients) {
            materialCost += GetMarketPrice(ingredient.Item) * ingredient.Quantity;
        }
        
        // Add labor cost based on production time
        float laborCost = recipe.ProductionTimeSeconds * LABOR_RATE_PER_SECOND;
        
        // Add skill premium for high-level recipes
        float skillPremium = 1.0f + (recipe.RequiredSkillLevel * 0.05f);
        
        return (materialCost + laborCost) * skillPremium;
    }
    
    // Market price lookup (simplified)
    private float GetMarketPrice(ItemType item) {
        // Reference economic system for current prices
        return EconomySystem.GetCurrentPrice(item);
    }
}
```

### Agent AI Integration

```csharp
public class AgentCraftingBehavior {
    // AI decides what to craft based on needs
    public Recipe SelectRecipeToCraft(Agent agent) {
        var needs = agent.GetCurrentNeeds();
        var availableRecipes = RecipeDatabase.GetAvailableRecipes(agent.SkillLevel);
        
        // Filter by needs
        if (needs.Hunger > 60) {
            return availableRecipes
                .Where(r => r.Category == RecipeCategory.Food)
                .OrderByDescending(r => r.Products.Sum(p => p.Quantity))
                .FirstOrDefault();
        }
        
        // Filter by economic opportunity
        if (agent.Personality.Greed > 70) {
            return availableRecipes
                .OrderByDescending(r => CalculateProfitMargin(r))
                .FirstOrDefault();
        }
        
        // Default: skill improvement
        return availableRecipes
            .Where(r => r.RequiredSkill == agent.PrimarySkill)
            .OrderBy(r => r.RequiredSkillLevel)
            .FirstOrDefault();
    }
}
```

---

## Summary

**Total Recipes Defined: 100**

| Category | Count | Primary Skill | Skill Range |
|----------|-------|---------------|-------------|
| Building Materials | 25 | Masonry | 0-6 |
| Tools | 20 | Toolmaking | 0-6 |
| Food & Cooking | 20 | Cooking | 0-5 |
| Processing | 20 | Smelting | 0-6 |
| Furniture | 15 | Carpentry | 0-4 |
| **Total** | **100** | - | - |

**Key Design Decisions:**
1. **Progressive Unlocking**: Recipes unlock with skill levels to create meaningful progression
2. **Tool Tiers**: Clear upgrade path from Stone → Iron → Steel with 3× and 10× durability improvements
3. **Batch Crafting**: Efficiency bonuses encourage mass production (up to 15% time savings)
4. **Quality System**: Skill levels 9-10 unlock Masterwork items with 50% effectiveness bonus
5. **Station Requirements**: Advanced recipes require advanced stations, creating infrastructure goals
6. **Fuel Mechanics**: Smelting and cooking require fuel, adding resource management depth

**Referenced Constants:**
- Stack sizes (wood: 100, stone: 50, food: 20, tools: 1)
- Production times (simple: 30s, complex: 120s, building: 300s)
- Tool durability (stone: 50, iron: 150, steel: 500)
- Quality multipliers (poor: 0.7, normal: 1.0, good: 1.15, excellent: 1.3, masterwork: 1.5)
- Skill time reduction (5% per level, 50% at level 10)

---

**Document End**
