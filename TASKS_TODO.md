SHIPPING NEXT (done in main, NOT released yet — the version bumps only on release day):
- TOWN siege drill: muster at an owned town (own rates: 10 wages / 14-day clock) + the LORD'S HALL storm when the walls fall but the defenders pull back — castles get the keep offer too, vanilla always had it armed (see AI_NOTES)

BUGS:
- [ ] Same bug, second door: no MCM = mod won't load either (since v1.0.0) — MCM satellite, own session + menu playtest (see AI_NOTES)
- [x] No "Select the battlefield" vs bandits + scout ride skipped its picker — starved scene pool, tiers added 2026.07.27 (see AI_NOTES)
- [ ] Orange Looters - Orange (train color) looters still are seen appearing after a mock castle or sea battle, but now they at least turn normal looters after a screen is opened and closed (line the inventory)
- [x] Battles near a VILLAGE are fought IN the village — ground pick greyed + named, scout ride goes into the village (built + PLAYTESTED OK 2026.07.28; bridges are fine, the game ships no bridge scene at all — see AI_NOTES)


NEXT UPDATE:
- [x] Playtest: CASTLE mock enemy (built 2026.07.26 — phantoms besiege or reinforce the garrison; see AI_NOTES)
- [x] Playtest: Steward speeds the cooldown + fighter companions instruct for XP (built 2026.07.26)
- [x] Renown/influence for the CASTLE drill: built 2026.07.25, scales with men on the field (see AI_NOTES; field/sea drills too? not decided)
- [x] Playtest: CASTLE siege drill - defense RAN 2026.07.26 (two battles, numbers verified); auto-resolve added same day (engines sit out, cheaper — see AI_NOTES), needs playtest
- [x] SEA scout ride SAILED 2026.07.26 - strip the crash-hunt debug spam (per-crewman log + reflection step-replication, see AI_NOTES), then normal playtest eyes
- [x] Playtest: ship divide GUI + phantom fleets at sea + siege/naval hour doors (all built, see AI_NOTES)
- [ ] Playtest: siege auto-resolve ATTACK road, round 3 — sim now right, but Done left vanilla's Attack/Leave menu armed (prisoners minted via Retreat; fixed 2026.07.26 eve: post-sim trigger + prisoner net, see AI_NOTES round 5) — check: Done goes straight home, no Attack/Leave, no prisoners; FIELD send-troops too (same muzzle broke it)
- [ ] Playtest eye: orange looters STILL seen after sieges (2nd fix 2026.07.25: delayed re-sweep + on-load heal + clan names now logged — if seen again, check training_battles.log [clan] lines)
- [x] Playtest check: one plain naval battle without training — loot/ships back to normal (see AI_NOTES)
- [ ] Playtest: TOWN drill both roads + storm the Lord's Hall (win, lose, retreat-and-storm-again) + a castle attack where 20+ defenders rout (the keep offer is new there too) (built 2026.08.02, see AI_NOTES)

NEXT UPDATEs or NOT FULLY DECIDED:
- [ ] Pull a near-village battle OUT of the village onto a chosen field? (changes real campaign consequences — your call; see AI_NOTES)
- [ ] Village green as a DRILL ground (needs the picker's candidate type widened — see AI_NOTES)
- [ ] Training while leading an ARMY (research done — army vs mock enemy first; see AI_NOTES)
- [ ] A menu door besides the hotkey (party-screen or clan-screen button)
- [ ] Scouting with companions (see AI_NOTES)
- [ ] War Horns CTD on scout ride — our teamless mission was the hole; teams added, shipped in v1.3.2 — ask the reporter to retest (see AI_NOTES)
