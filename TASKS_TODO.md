BUGS:
- [ ] Orange Looters - Orange (train color) looters still are seen appearing after a mock castle or sea battle, but now they at least turn normal looters after a screen is opened and closed (line the inventory)


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

NEXT UPDATEs or NOT FULLY DECIDED:
- [ ] TOWN siege training — castles landed 2026.07.25; towns ride the same code (own pay/cooldown, Lord's Hall stage; see AI_NOTES)
- [ ] Training while leading an ARMY (research done — army vs mock enemy first; see AI_NOTES)
- [ ] A menu door besides the hotkey (party-screen or clan-screen button)
- [ ] Scouting with companions (see AI_NOTES)
- [ ] War Horns mod CTD on scout ride (Nexus report 2026.07.26) — likely their mission hook assumes battle teams; decompile War Horns, maybe guard our side (not crucial)
