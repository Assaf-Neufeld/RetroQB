using System.Numerics;
using RetroQB.Entities;
using static RetroQB.Routes.RouteType;

namespace RetroQB.Gameplay;

/// <summary>Assigns formation-specific geometry and reads by roster identity, not array position.</summary>
internal static class PassingConceptAuthor
{
    public static PlayDefinition Author(string id, string name, FormationDefinition formation, PlaybookInfo info,
        IReadOnlyDictionary<ReceiverSlot, PlayerAssignment> original, BackfieldSequence? backfield = null)
    {
        var jobs = original.ToDictionary();
        var positions = formation.AlignmentSlots.Select((slot, i) => (slot, point: formation.Alignment.SkillPositions[i]))
            .ToDictionary(p => p.slot, p => p.point);
        var wide = positions.Where(p => p.Key.IsWideReceiverSlot()).OrderBy(p => p.Value.XFraction).Select(p => p.Key).ToArray();
        var left = wide[0];
        var right = wide[^1];
        int side = positions.Count(p => !p.Key.IsRunningBackSlot() && p.Value.XFraction > .5f)
            >= positions.Count(p => !p.Key.IsRunningBackSlot() && p.Value.XFraction < .5f) ? 1 : -1;
        var outer = side > 0 ? right : left;
        var opposite = side > 0 ? left : right;
        var inside = positions.Where(p => !p.Key.IsRunningBackSlot() && p.Key != left && p.Key != right)
            .OrderByDescending(p => side * p.Value.XFraction).Select(p => p.Key).ToArray();
        ReceiverSlot middle = inside[0];
        ReceiverSlot support = inside.Length > 1 ? inside[^1] : positions.Keys.First(s => s != left && s != right && s != middle);
        ReceiverSlot outlet = positions.Keys.Contains(ReceiverSlot.RB1) ? ReceiverSlot.RB1 : support;
        var reads = new List<PassRead>();
        string concept = id[(id.LastIndexOf('.') + 1)..];
        float center = Constants.FieldWidth * .5f;
        float edge = side > 0 ? Constants.FieldWidth - 3 : 3;
        // Defaults keep every remaining receiver purposeful while concept roles override them below.
        foreach (var slot in positions.Keys.Where(s => jobs[s].Role == AssignmentRole.Route))
            Route(slot, slot.IsRunningBackSlot() ? Flat : Seam, slot.IsRunningBackSlot()
                ? [(center - side * 9, 2)] : [(X(slot), 10), (X(slot), 24)], read: 1);

        switch (concept)
        {
            case "mesh": case "mesh-wheel":
                Route(left, Drag, [(X(left) + 2, 3.5f), (Constants.FieldWidth - 6, 3.5f)], read: 1);
                Route(right, Drag, [(X(right) - 2, 5.5f), (6, 5.5f)], delay: .12f, read: 1);
                Route(middle, Corner, [(X(middle), 9), (edge, 17)]);
                if (support != outlet) Route(support, Curl, [(X(support), 10), (X(support) - side * 2, 8)], settle: true);
                Route(outlet, concept == "mesh-wheel" ? Wheel : Flat,
                    concept == "mesh-wheel" ? [(edge - side * 2, 2), (edge - side * 2, 10), (edge - side * 2, 22)]
                        : [(edge, 1.5f)], read: 1);
                Read(left, .25f, 1); Read(right, .35f, 1); Read(middle, .8f, 1); Read(outlet, .45f, 1, true);
                break;
            case "flood": case "sail": case "pa-boot":
                Route(outer, Go, [(X(outer), 14), (edge - side * 1.5f, 26)], read: 1);
                Route(middle, Corner, [(X(middle), 7), (edge - side * 2, 13)]);
                Route(opposite, Comeback, [(X(opposite), 13), (X(opposite) + side * 2, 9)], settle: true);
                Route(support, Flat, [(edge, 2)], read: 1);
                if (outlet != support) Route(outlet, Flat, [(center - side * 9, 2)], read: 1);
                Read(middle, .55f, 1); Read(support, .2f, 1, true); Read(outer, 1, 1); Read(opposite, .8f, 1);
                break;
            case "stick": case "spacing":
                Route(left, Hitch, [(X(left), 5), (X(left), 4.5f)], settle: true);
                Route(right, Hitch, [(X(right), 5), (X(right), 4.5f)], settle: true);
                Route(middle, Hitch, [(X(middle), 6), (X(middle) + side * 1.5f, 5.5f)], settle: true);
                Route(support, Flat, [(edge, 1.5f)], read: 1);
                if (outlet != support) Route(outlet, Angle, [(center - side * 5, 1), (center, 4)], settle: true);
                Read(middle, .2f, 1); Read(support, .15f, 1, true); Read(outer, .25f, 1); Read(opposite, .25f, 1);
                break;
            case "drive": case "levels": case "deep-ins": case "combo":
                Route(left, concept == "drive" ? Drag : InShallow,
                    [(X(left), concept == "drive" ? 3 : 6), (center + 8, concept == "drive" ? 3 : 6)], read: 1);
                Route(right, InDeep, [(X(right), 13), (center - 8, 13)], read: 1);
                Route(middle, Seam, [(X(middle), 12), (X(middle), 24)], read: 1);
                if (support != outlet) Route(support, Curl, [(X(support), 9), (X(support) - side, 7)], settle: true);
                if (jobs[outlet].Role == AssignmentRole.Route) Route(outlet, Flat, [(center - side * 10, 2)], read: 1);
                Read(right, .65f, 1); Read(left, .2f, 1); Read(middle, .9f, 1); Read(outlet, .35f, 1, true);
                break;
            case "scissors": case "mills": case "pa-cross": case "pa-deep":
                Route(outer, PostDeep, [(X(outer), 14), (center - side * 6, 24)], read: 1);
                Route(opposite, InDeep, [(X(opposite), 12), (center + side * 10, 12)], read: 1);
                Route(middle, concept == "scissors" ? Corner : Seam,
                    [(X(middle), 9), (concept == "scissors" ? edge : X(middle), 23)], read: 1);
                if (support != outlet && jobs[support].Role == AssignmentRole.Route)
                    Route(support, Curl, [(X(support), 7), (X(support) - side * 2, 5)], settle: true);
                if (jobs[outlet].Role == AssignmentRole.Route)
                    Route(outlet, concept == "pa-cross" ? Wheel : Flat,
                        concept == "pa-cross" ? [(center - side * 7, 2), (center - side * 7, 8), (center - side * 7, 20)]
                            : [(center - side * 9, 2)], read: 1);
                Read(outer, 1.1f, 1); Read(opposite, .7f, 1); Read(middle, .9f, 1); Read(outlet, .45f, 1, true);
                break;
            case "four-verts":
                foreach (var slot in positions.Keys)
                    Route(slot, Go, [(X(slot), 12), (X(slot), 28)], read: 1);
                Read(middle, .8f, 1); Read(outer, 1, 1); Read(opposite, 1, 1);
                break;
            case "smash":
                Route(outer, Hitch, [(X(outer), 5), (X(outer), 4.5f)], settle: true);
                Route(middle, Corner, [(X(middle), 8), (edge, 16)], read: 1);
                Route(opposite, Go, [(X(opposite), 12), (X(opposite), 24)], read: 1);
                Read(middle, .65f, 1); Read(outer, .2f, 1, true); Read(opposite, 1, 1);
                break;
            default: // Bunch Quick and Slant Flat: same-side slant/flat with a vertical clear-out.
                Route(outer, Slant, [(X(outer) - side * 8, 8)], read: 1);
                Route(middle, Flat, [(edge, 2)], read: 1);
                Route(opposite, Slant, [(X(opposite) + side * 8, 9)], read: 1);
                if (support != outlet) Route(support, Go, [(X(support), 12), (X(support), 24)], read: 1);
                if (jobs[outlet].Role == AssignmentRole.Route) Route(outlet, Flat, [(center - side * 9, 2)], read: 1);
                Read(outer, .15f, 1); Read(middle, .15f, 1, true); Read(opposite, .25f, 1); Read(outlet, .3f, 1, true);
                break;
        }

        bool maxProtect = concept is "scissors" or "mills";
        if (maxProtect)
        {
            // A vertical shot deliberately sacrifices its back and one inside outlet.
            foreach (var slot in new[] { support, outlet }.Distinct())
                if ((slot.IsRunningBackSlot() || slot.IsTightEndSlot())
                    && (slot == outlet || formation.Personnel.Id != "20")) jobs[slot] = Protect(slot, permanent: true);
        }
        else if (support.IsRunningBackSlot() || (backfield?.Action == BackfieldAction.PlayAction && support.IsTightEndSlot()))
            jobs[support] = Protect(support, permanent: false);
        reads.RemoveAll(r => jobs[r.Slot].Role != AssignmentRole.Route);
        foreach (var slot in positions.Keys.Where(s => jobs[s].Role == AssignmentRole.Route))
            if (!reads.Any(r => r.Slot == slot)) Read(slot, .4f, 1, slot.IsRunningBackSlot());
        var mode = maxProtect ? ProtectionMode.MaxProtect : concept is "flood" or "sail" or "pa-boot"
            ? side < 0 ? ProtectionMode.SlideLeft : ProtectionMode.SlideRight : ProtectionMode.Balanced;
        return new(id, name, PlayType.Pass, formation, jobs, backfield: backfield, info: info,
            reads: reads, protection: new(mode, maxProtect && jobs[support].Role == AssignmentRole.Block ? support : null));

        float X(ReceiverSlot slot) => positions[slot].XFraction * Constants.FieldWidth;
        void Route(ReceiverSlot slot, RouteType type, (float X, float Y)[] path, bool settle = false, float delay = 0, int? read = null)
        {
            // Permanent legacy protectors retain their role until a concept explicitly makes them a target.
            if (jobs[slot].Role == AssignmentRole.Block) return;
            float originX = X(slot), depth = positions[slot].Depth;
            if (backfield?.Participant == slot)
            { originX = center + backfield.MeshOffset.X; depth = formation.Alignment.Quarterback.Depth - backfield.MeshOffset.Y; }
            var geometry = new RouteDefinition(path.Select(p => new RouteStep(new(p.X - originX, p.Y + depth))),
                settle ? RouteFinish.Settle : RouteFinish.Continue, delay, read, compressToField: true,
                fieldDepthBudget: 28, originDepth: depth);
            jobs[slot] = jobs[slot] with { Route = type, RouteSide = 1, RouteDefinition = geometry };
        }
        void Read(ReceiverSlot slot, float time, int step, bool checkdown = false)
        {
            if (jobs[slot].Role == AssignmentRole.Route && !reads.Any(r => r.Slot == slot))
                reads.Add(new(slot, time, step, checkdown));
        }
        PlayerAssignment Protect(ReceiverSlot slot, bool permanent)
        {
            var job = new BlockingAssignment(BlockingJob.PassProtection, new(0, -positions[slot].Depth));
            return permanent ? new(AssignmentRole.Block, Blocking: job) : jobs[slot] with
            { Blocking = job, ReleaseAfterSeconds = backfield?.Action == BackfieldAction.PlayAction ? 1.1f : .65f,
                ProtectionRelease = new(2.5f) };
        }
    }
}
