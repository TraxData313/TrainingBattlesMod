using System;
using System.Collections.Generic;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The pure arithmetic of dividing a fleet for a naval training battle. V1 has no "divide
    /// the ships" GUI (that picker is the next release's must) — instead the fleet auto-splits
    /// PROPORTIONAL TO CREW: whatever share of the healthy men the opposing half took in the
    /// picker, it gets about that share of the fleet's crew capacity to sail. The flagship —
    /// the caller decides which hull that is — always stays with the player. No game types;
    /// fully unit-tested.
    /// </summary>
    public static class FleetSplitMath
    {
        /// <summary>
        /// Which ships the OPPONENT half sails. Deterministic greedy fill: ships are dealt
        /// largest-crew first, each to the side whose capacity is furthest below its target
        /// share (ties go to the player). Both sides are guaranteed at least one hull —
        /// with fewer than two ships there is no naval drill, and the caller must not ask.
        /// </summary>
        /// <param name="crewCapacities">Each ship's crew capacity, in fleet order. At least 2 entries.</param>
        /// <param name="flagshipIndex">The hull pinned to the player's side (out-of-range = no pin).</param>
        /// <param name="playerCrew">Healthy men staying with the player (≥ 1).</param>
        /// <param name="opponentCrew">Healthy men of the opposing half (≥ 1).</param>
        /// <returns>Indices into <paramref name="crewCapacities"/> for the opponent, ascending.</returns>
        public static List<int> OpponentShips(IReadOnlyList<int> crewCapacities, int flagshipIndex,
            int playerCrew, int opponentCrew)
        {
            if (crewCapacities == null) throw new ArgumentNullException(nameof(crewCapacities));
            if (crewCapacities.Count < 2)
                throw new ArgumentException("A naval drill needs at least two hulls.", nameof(crewCapacities));

            var totalCrew = Math.Max(playerCrew, 0) + Math.Max(opponentCrew, 0);
            var opponentShare = totalCrew > 0 ? Math.Max(opponentCrew, 0) / (double)totalCrew : 0.5;

            var totalCapacity = 0;
            for (var i = 0; i < crewCapacities.Count; i++) totalCapacity += Math.Max(crewCapacities[i], 0);
            var opponentTarget = totalCapacity * opponentShare;
            var playerTarget = totalCapacity - opponentTarget;

            // Largest hulls first; equal hulls keep fleet order so the same fleet splits the
            // same way every drill.
            var order = new List<int>();
            for (var i = 0; i < crewCapacities.Count; i++) order.Add(i);
            order.Sort((a, b) => crewCapacities[b] != crewCapacities[a]
                ? crewCapacities[b].CompareTo(crewCapacities[a])
                : a.CompareTo(b));

            var opponent = new List<int>();
            double playerFilled = 0, opponentFilled = 0;
            foreach (var index in order)
            {
                var capacity = Math.Max(crewCapacities[index], 0);
                if (index == flagshipIndex)
                {
                    playerFilled += capacity; // the flagship never crosses
                    continue;
                }
                // Give the hull to the side missing the larger piece of its target; a tie is
                // the player's (their side also carries the player character).
                var playerDeficit = playerTarget - playerFilled;
                var opponentDeficit = opponentTarget - opponentFilled;
                if (opponentDeficit > playerDeficit)
                {
                    opponent.Add(index);
                    opponentFilled += capacity;
                }
                else
                {
                    playerFilled += capacity;
                }
            }

            // Both sides sail or nobody drills: the opponent must hold at least one hull (never
            // the flagship), and the player must keep at least one too.
            if (opponent.Count == 0)
                opponent.Add(SmallestNonFlagship(crewCapacities, flagshipIndex, opponent, invert: false));
            else if (opponent.Count == crewCapacities.Count)
                opponent.Remove(SmallestNonFlagship(crewCapacities, flagshipIndex, opponent, invert: true));

            opponent.Sort();
            return opponent;
        }

        /// <summary>The smallest hull eligible to move: when <paramref name="invert"/> is false,
        /// the smallest PLAYER hull that is not the flagship (to hand across); when true, the
        /// smallest OPPONENT hull (to hand back).</summary>
        private static int SmallestNonFlagship(IReadOnlyList<int> crewCapacities, int flagshipIndex,
            List<int> opponent, bool invert)
        {
            var best = -1;
            for (var i = 0; i < crewCapacities.Count; i++)
            {
                if (i == flagshipIndex && !invert) continue;
                if (opponent.Contains(i) != invert) continue;
                if (best < 0 || crewCapacities[i] < crewCapacities[best]) best = i;
            }
            return best;
        }
    }
}
