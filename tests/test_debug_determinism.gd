extends "res://tests/test_case.gd"

# const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
    subtest("Find Divergence Tick", _find_divergence)

func _find_divergence() -> void:
    _log_trace("Starting...")
    var Fixtures = load("res://tests/test_fixtures.gd")
    var seed_val := 12345
    _log_trace("Creating sim1...")
    var sim1 = Fixtures.make_sim(seed_val)
    _log_trace("Creating sim2...")
    var sim2 = Fixtures.make_sim(seed_val)
    
    _log_trace("Sim1 Dims: %d x %d" % [sim1.state.world.width, sim1.state.world.height])
    _log_trace("Sim2 Dims: %d x %d" % [sim2.state.world.width, sim2.state.world.height])
    
    # Check RNG consistency directly by creating fresh RNGs with same seed
    var r1 = sim1.state.rng
    var r2 = sim2.state.rng
    _log_trace("RNG1 State: " + str(r1.get_state()))
    _log_trace("RNG2 State: " + str(r2.get_state()))
    
    # We can't advance them because that would ruin the test state comparison?
    # But the state mismatch is already there (from init_new execution).
    # So init_new execution ALREADY diverged.


    _log_trace("Calculating checksum sim1...")
    var c1 = sim1.checksum()
    _log_trace("Calculating checksum sim2...")
    var c2 = sim2.checksum()
    _log_trace("Checksums calculated: " + c1 + " vs " + c2)
    
    if c1 != c2:
        _log_trace("Mismatch found! Checking components...")
        
        var w1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.world.to_dict()))
        var w2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.world.to_dict()))
        if w1 != w2: 
            _log_trace("Mismatch in WORLD")
            # Drill down
            var wd1 = sim1.state.world.to_dict()
            var wd2 = sim2.state.world.to_dict()
            
            if Serializers.sha256_hash(Serializers.to_sorted_json(wd1.get("tiles", []))) != Serializers.sha256_hash(Serializers.to_sorted_json(wd2.get("tiles", []))):
                _log_trace("  Mismatch in WORLD.TILES")
            
            var rn1 = wd1.get("resource_nodes", [])
            var rn2 = wd2.get("resource_nodes", [])
            if Serializers.sha256_hash(Serializers.to_sorted_json(rn1)) != Serializers.sha256_hash(Serializers.to_sorted_json(rn2)):
                _log_trace("  Mismatch in WORLD.RESOURCE_NODES")
                _log_trace("    Count: " + str(rn1.size()) + " vs " + str(rn2.size()))
                if rn1.size() > 0 and rn2.size() > 0:
                    _log_trace("    First Node: " + str(rn1[0]) + " vs " + str(rn2[0]))
            
            # Check Tuning
            var t1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.tuning))
            var t2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.tuning))
            if t1 != t2:
                _log_trace("Mismatch in TUNING")
            else:
                 _log_trace("Tuning matches")
                
            if Serializers.sha256_hash(Serializers.to_sorted_json(wd1.get("workshops", []))) != Serializers.sha256_hash(Serializers.to_sorted_json(wd2.get("workshops", []))):
                _log_trace("  Mismatch in WORLD.WORKSHOPS")
            
            if Serializers.sha256_hash(Serializers.to_sorted_json(wd1.get("pollution", []))) != Serializers.sha256_hash(Serializers.to_sorted_json(wd2.get("pollution", []))):
                _log_trace("  Mismatch in WORLD.POLLUTION")

        var m1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.market.to_dict()))
        var m2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.market.to_dict()))
        if m1 != m2: _log_trace("Mismatch in MARKET")
        
        var ct1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.contracts_system.to_dict()))
        var ct2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.contracts_system.to_dict()))
        if ct1 != ct2: _log_trace("Mismatch in CONTRACTS")
        
        var f1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.factions_system.to_dict()))
        var f2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.factions_system.to_dict()))
        if f1 != f2: _log_trace("Mismatch in FACTIONS")
        
        var rng1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.rng.get_state()))
        var rng2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.rng.get_state()))
        if rng1 != rng2: _log_trace("Mismatch in RNG")
        
        var a1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.agents)) # Agents is array of objects, need to serialize first?
        # SimState.to_dict handles agents serialization. Let's do manual loop.
        if sim1.state.agents.size() != sim2.state.agents.size():
            _log_trace("Mismatch in AGENT COUNT")
        else:
             for i in range(sim1.state.agents.size()):
                 var ag1 = Serializers.sha256_hash(Serializers.to_sorted_json(sim1.state.agents[i].to_dict()))
                 var ag2 = Serializers.sha256_hash(Serializers.to_sorted_json(sim2.state.agents[i].to_dict()))
                 if ag1 != ag2: _log_trace("Mismatch in AGENT " + str(sim1.state.agents[i].id))

        fail("Mismatch detail logged")
    
func _log_trace(msg: String) -> void:
    var f = FileAccess.open("C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies/debug_trace.txt", FileAccess.READ_WRITE)
    if not f: f = FileAccess.open("C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies/debug_trace.txt", FileAccess.WRITE)
    f.seek_end()
    f.store_line(msg)
    f.close()
