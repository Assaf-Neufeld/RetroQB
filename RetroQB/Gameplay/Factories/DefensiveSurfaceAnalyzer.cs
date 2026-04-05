using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal static class DefensiveSurfaceAnalyzer
{
    private const float DetachedReceiverMaxDepth = 1.45f;
    private const float AttachedTightEndMaxDepth = 1.1f;
    private const float AttachedTightEndMaxOffsetFromCenter = 11.75f;

    public static OffensiveSurface Analyze(IReadOnlyList<Receiver> receivers, float lineOfScrimmage)
    {
        List<Receiver> eligible = receivers
            .Where(receiver => receiver.Eligible)
            .OrderBy(receiver => receiver.Position.X)
            .ToList();

        if (eligible.Count == 0)
        {
            eligible = receivers
                .OrderBy(receiver => receiver.Position.X)
                .ToList();
        }

        if (eligible.Count == 0)
        {
            return new OffensiveSurface(0, 0, 0, 0, 0, 0, 0, 0, -1, -1, -1, -1, -1, Constants.FieldWidth * 0.50f, FormationStrength.Balanced);
        }

        float fieldMidX = Constants.FieldWidth * 0.50f;
        List<Receiver> attachedTightEnds = eligible
            .Where(receiver => IsAttachedTightEnd(receiver, lineOfScrimmage, fieldMidX))
            .ToList();

        List<Receiver> detached = eligible
            .Where(receiver => !attachedTightEnds.Contains(receiver))
            .Where(receiver => lineOfScrimmage - receiver.Position.Y <= DetachedReceiverMaxDepth)
            .OrderBy(receiver => receiver.Position.X)
            .ToList();

        if (detached.Count == 0)
        {
            detached = eligible
                .Where(receiver => !attachedTightEnds.Contains(receiver))
                .OrderBy(receiver => receiver.Position.X)
                .ToList();

            if (detached.Count == 0)
            {
                detached = eligible;
            }
        }

        List<Receiver> lineStructure = eligible
            .Where(receiver => lineOfScrimmage - receiver.Position.Y <= DetachedReceiverMaxDepth || attachedTightEnds.Contains(receiver))
            .OrderBy(receiver => receiver.Position.X)
            .ToList();

        if (lineStructure.Count == 0)
        {
            lineStructure = eligible;
        }

        const float middleBandWidth = 0.85f;

        List<Receiver> left = detached
            .Where(receiver => receiver.Position.X < fieldMidX - middleBandWidth)
            .ToList();
        List<Receiver> right = detached
            .Where(receiver => receiver.Position.X > fieldMidX + middleBandWidth)
            .ToList();
        List<Receiver> middle = detached
            .Where(receiver => MathF.Abs(receiver.Position.X - fieldMidX) <= middleBandWidth)
            .ToList();

        int leftWide = left.Count > 0 ? left[0].Index : -1;
        int leftInside = left.Count > 0 ? left[^1].Index : (middle.Count > 0 ? middle[0].Index : -1);
        int middleReceiver = middle.Count > 0 ? middle[middle.Count / 2].Index : -1;
        int rightInside = right.Count > 0 ? right[0].Index : (middle.Count > 0 ? middle[^1].Index : -1);
        int rightWide = right.Count > 0 ? right[^1].Index : -1;

        int leftAttachedTightEnds = attachedTightEnds.Count(receiver => receiver.Position.X < fieldMidX);
        int rightAttachedTightEnds = attachedTightEnds.Count(receiver => receiver.Position.X > fieldMidX);
        int leftDetachedTightEnds = left.Count(receiver => receiver.IsTightEnd);
        int rightDetachedTightEnds = right.Count(receiver => receiver.IsTightEnd);
        FormationStrength strength = DetermineFormationStrength(
            left,
            right,
            leftDetachedTightEnds + leftAttachedTightEnds,
            rightDetachedTightEnds + rightAttachedTightEnds,
            fieldMidX);
        float centerX = lineStructure.Average(receiver => receiver.Position.X);

        return new OffensiveSurface(
            detached.Count,
            left.Count,
            right.Count,
            detached.Count(receiver => receiver.IsTightEnd),
            attachedTightEnds.Count,
            leftAttachedTightEnds,
            rightAttachedTightEnds,
            Math.Max(0, eligible.Count - detached.Count),
            leftWide,
            leftInside,
            middleReceiver,
            rightInside,
            rightWide,
            centerX,
            strength);
    }

    private static FormationStrength DetermineFormationStrength(
        IReadOnlyList<Receiver> left,
        IReadOnlyList<Receiver> right,
        int leftTightEnds,
        int rightTightEnds,
        float fieldMidX)
    {
        int detachedCount = left.Count + right.Count;
        bool heavyDetachedSurface = detachedCount <= 2;

        // In heavy looks, attached TE surface should still set strong side.
        if (heavyDetachedSurface && leftTightEnds != rightTightEnds)
        {
            return leftTightEnds > rightTightEnds ? FormationStrength.Left : FormationStrength.Right;
        }

        // For spread/base pass looks, detached receiver distribution defines strength first.
        if (left.Count != right.Count)
        {
            return left.Count > right.Count ? FormationStrength.Left : FormationStrength.Right;
        }

        // Tie-break by inside detached leverage toward midfield.
        float leftInsideDistance = left.Count > 0 ? fieldMidX - left[^1].Position.X : float.MaxValue;
        float rightInsideDistance = right.Count > 0 ? right[0].Position.X - fieldMidX : float.MaxValue;

        if (leftInsideDistance != rightInsideDistance)
        {
            return leftInsideDistance < rightInsideDistance ? FormationStrength.Left : FormationStrength.Right;
        }

        // Final tie-break: attached TE distribution.
        if (leftTightEnds != rightTightEnds)
        {
            return leftTightEnds > rightTightEnds ? FormationStrength.Left : FormationStrength.Right;
        }

        return FormationStrength.Balanced;
    }

    private static bool IsAttachedTightEnd(Receiver receiver, float lineOfScrimmage, float fieldMidX)
    {
        if (!receiver.IsTightEnd)
        {
            return false;
        }

        float depthFromLine = lineOfScrimmage - receiver.Position.Y;
        if (depthFromLine > AttachedTightEndMaxDepth)
        {
            return false;
        }

        return MathF.Abs(receiver.Position.X - fieldMidX) <= AttachedTightEndMaxOffsetFromCenter;
    }
}
