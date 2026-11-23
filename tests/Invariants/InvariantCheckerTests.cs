using System.Linq;
using BT.Core.Domain;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Invariants;

public sealed class InvariantCheckerTests
{
    [Theory]
    [InlineData(72, 72, 0)]
    [InlineData(72, 71, 1)]
    [InlineData(99, 99, 0)]
    [InlineData(100, 99, 2)]
    public void PlayerRatingRangesAndPotentialAreChecked(int ovr, int potential, int expectedIssues)
    {
        var player = CreatePlayer(potential);
        Assert.Equal(expectedIssues, InvariantChecker.CheckPlayerRating(player, ovr).Count);
    }

    [Fact]
    public void SquadRequiresTwoPlayersInEveryLine()
    {
        var registrations = new[]
        {
            Registration("g1", Position.Goalkeeper), Registration("g2", Position.Goalkeeper),
            Registration("d1", Position.CentreBack), Registration("d2", Position.LeftBack),
            Registration("m1", Position.CentralMidfielder), Registration("m2", Position.DefensiveMidfielder),
            Registration("f1", Position.Striker)
        };

        Assert.Contains(InvariantChecker.CheckMinimumSquad(registrations, "club"), issue => issue.Contains("forwards"));
        Assert.Empty(InvariantChecker.CheckMinimumSquad(registrations.Append(Registration("f2", Position.RightWinger)), "club"));
    }

    private static ClubRegistration Registration(string playerId, Position position) => new(playerId, "club", position);

    private static Player CreatePlayer(int potential)
    {
        var visible = System.Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().ToDictionary(attribute => attribute, _ => 70);
        var hidden = System.Enum.GetValues(typeof(HiddenAttribute)).Cast<HiddenAttribute>().ToDictionary(attribute => attribute, _ => 70);
        hidden[HiddenAttribute.Potential] = potential;
        return new Player("p", visible, hidden, 22, "CL", new[] { Position.Striker }, null);
    }
}
