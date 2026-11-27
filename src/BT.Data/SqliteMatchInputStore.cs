using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BT.Core.Domain;
using BT.Core.Sim;
using Microsoft.Data.Sqlite;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Data;

/// <summary>Persists prepared, immutable simulation inputs alongside a scheduled fixture.</summary>
public sealed class SqliteMatchInputStore
{
    private readonly SqliteConnection _connection;

    public SqliteMatchInputStore(SqliteConnection connection) =>
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    /// <summary>Stores caller-resolved match inputs and the exact competition rules snapshot.</summary>
    public void Save(string fixtureId, string competitionId, string competitionRulesJson, MatchInput input)
    {
        if (string.IsNullOrWhiteSpace(fixtureId)) throw new ArgumentException("A fixture id is required.", nameof(fixtureId));
        if (string.IsNullOrWhiteSpace(competitionId)) throw new ArgumentException("A competition id is required.", nameof(competitionId));
        if (string.IsNullOrWhiteSpace(competitionRulesJson)) throw new ArgumentException("Competition rules JSON is required.", nameof(competitionRulesJson));
        if (input is null) throw new ArgumentNullException(nameof(input));

        using var transaction = _connection.BeginTransaction(deferred: false);
        try
        {
            using (var command = Create(transaction, "DELETE FROM fixture_match_context WHERE fixture_id=$fixture;"))
            { command.Parameters.AddWithValue("$fixture", fixtureId); command.ExecuteNonQuery(); }
            using (var command = Create(transaction, @"INSERT INTO fixture_match_context
(fixture_id,competition_id,competition_rules_json,seed,engine_version,is_neutral_venue)
VALUES($fixture,$competition,$rules,$seed,$engine,$neutral);"))
            {
                command.Parameters.AddWithValue("$fixture", fixtureId);
                command.Parameters.AddWithValue("$competition", competitionId);
                command.Parameters.AddWithValue("$rules", competitionRulesJson);
                command.Parameters.AddWithValue("$seed", input.Seed.ToString(CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$engine", (int)input.EngineVersion);
                command.Parameters.AddWithValue("$neutral", input.IsNeutralVenue ? 1 : 0);
                command.ExecuteNonQuery();
            }
            SaveTeam(transaction, fixtureId, input.HomeTeam, true);
            SaveTeam(transaction, fixtureId, input.AwayTeam, false);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>Rebuilds a complete input from its persisted snapshot; missing or corrupt data is an error.</summary>
    public MatchInput Load(string fixtureId) => LoadPrepared(fixtureId).Input;

    /// <summary>Loads the simulation input together with its competition identity and exact rules snapshot.</summary>
    public PersistedMatchInput LoadPrepared(string fixtureId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT competition_id,competition_rules_json,seed,engine_version,is_neutral_venue FROM fixture_match_context WHERE fixture_id=$fixture;";
        command.Parameters.AddWithValue("$fixture", fixtureId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidOperationException($"No prepared match input exists for fixture '{fixtureId}'.");
        var competitionId = reader.GetString(0);
        var competitionRulesJson = reader.GetString(1);
        if (!ulong.TryParse(reader.GetString(2), NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
            throw new InvalidOperationException($"Invalid persisted seed for fixture '{fixtureId}'.");
        var engine = (EngineVersion)reader.GetInt32(3);
        var neutral = reader.GetInt32(4) != 0;
        reader.Close();
        var teams = LoadTeams(fixtureId);
        var homeTeams = teams.Where(team => team.IsHome).ToArray();
        var awayTeams = teams.Where(team => !team.IsHome).ToArray();
        if (homeTeams.Length != 1 || awayTeams.Length != 1)
            throw new InvalidOperationException("Persisted match input must have exactly one home and one away team.");
        var home = homeTeams[0]; var away = awayTeams[0];
        var input = new MatchInput(LoadTeam(fixtureId, home.ClubId), LoadTeam(fixtureId, away.ClubId), seed, engine, neutral);
        return new PersistedMatchInput(competitionId, competitionRulesJson, input);
    }

    private void SaveTeam(SqliteTransaction transaction, string fixtureId, MatchTeamInput team, bool home)
    {
        using (var command = Create(transaction, @"INSERT INTO fixture_match_teams
(fixture_id,club_id,is_home,formation_id,mentality,passing,pressing,defensive_line,width)
VALUES($fixture,$club,$home,$formation,$mentality,$passing,$pressing,$line,$width);"))
        {
            command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", team.TeamId);
            command.Parameters.AddWithValue("$home", home ? 1 : 0); command.Parameters.AddWithValue("$formation", team.Tactics.FormationId);
            command.Parameters.AddWithValue("$mentality", team.Tactics.Mentality.ToString()); command.Parameters.AddWithValue("$passing", team.Tactics.Passing.ToString());
            command.Parameters.AddWithValue("$pressing", team.Tactics.Pressing.ToString()); command.Parameters.AddWithValue("$line", team.Tactics.DefensiveLine.ToString());
            command.Parameters.AddWithValue("$width", team.Tactics.Width.ToString()); command.ExecuteNonQuery();
        }
        SavePlayers(transaction, fixtureId, team.TeamId, team.Starters, true);
        SavePlayers(transaction, fixtureId, team.TeamId, team.Substitutes, false);
    }

    private void SavePlayers(SqliteTransaction transaction, string fixtureId, string clubId, IReadOnlyList<MatchPlayerInput> players, bool starter)
    {
        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            using (var command = Create(transaction, @"INSERT INTO fixture_match_players
(fixture_id,club_id,player_id,is_starter,selection_order,position,fitness,morale,shirt_number,is_extra_community,is_injured,is_suspended)
VALUES($fixture,$club,$player,$starter,$order,$position,$fitness,$morale,$shirt,$extra,$injured,$suspended);"))
            {
                command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", clubId); command.Parameters.AddWithValue("$player", p.PlayerId);
                command.Parameters.AddWithValue("$starter", starter ? 1 : 0); command.Parameters.AddWithValue("$order", i + 1); command.Parameters.AddWithValue("$position", p.Position.ToString());
                command.Parameters.AddWithValue("$fitness", p.Fitness); command.Parameters.AddWithValue("$morale", p.Morale);
                command.Parameters.AddWithValue("$shirt", (object?)p.ShirtNumber ?? DBNull.Value); command.Parameters.AddWithValue("$extra", p.IsExtraCommunity ? 1 : 0);
                command.Parameters.AddWithValue("$injured", p.IsInjured ? 1 : 0); command.Parameters.AddWithValue("$suspended", p.IsSuspended ? 1 : 0); command.ExecuteNonQuery();
            }
            foreach (var attribute in p.EffectiveAttributes.OrderBy(pair => pair.Key))
            {
                using var command = Create(transaction, "INSERT INTO fixture_match_player_attributes VALUES($fixture,$club,$player,$attribute,$value);");
                command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", clubId); command.Parameters.AddWithValue("$player", p.PlayerId);
                command.Parameters.AddWithValue("$attribute", attribute.Key.ToString()); command.Parameters.AddWithValue("$value", attribute.Value); command.ExecuteNonQuery();
            }
        }
    }

    private IReadOnlyList<(string ClubId, bool IsHome)> LoadTeams(string fixtureId)
    {
        using var command = _connection.CreateCommand(); command.CommandText = "SELECT club_id,is_home FROM fixture_match_teams WHERE fixture_id=$fixture ORDER BY is_home DESC;";
        command.Parameters.AddWithValue("$fixture", fixtureId); using var reader = command.ExecuteReader();
        var result = new List<(string, bool)>(); while (reader.Read()) result.Add((reader.GetString(0), reader.GetInt32(1) == 1)); return result;
    }

    private MatchTeamInput LoadTeam(string fixtureId, string clubId)
    {
        string formation; TacticalMentality mentality; PassingStyle passing; PressingStyle pressing; DefensiveLine line; TeamWidth width;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT formation_id,mentality,passing,pressing,defensive_line,width FROM fixture_match_teams WHERE fixture_id=$fixture AND club_id=$club;";
            command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", clubId); using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException($"Missing persisted tactics for club '{clubId}'.");
            formation = reader.GetString(0); mentality = Parse<TacticalMentality>(reader.GetString(1)); passing = Parse<PassingStyle>(reader.GetString(2));
            pressing = Parse<PressingStyle>(reader.GetString(3)); line = Parse<DefensiveLine>(reader.GetString(4)); width = Parse<TeamWidth>(reader.GetString(5));
        }
        var starters = new List<MatchPlayerInput>(); var substitutes = new List<MatchPlayerInput>();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = @"SELECT player_id,is_starter,position,fitness,morale,shirt_number,is_extra_community,is_injured,is_suspended
FROM fixture_match_players WHERE fixture_id=$fixture AND club_id=$club ORDER BY is_starter DESC,selection_order;";
            command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", clubId); using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0); var starter = reader.GetInt32(1) == 1; var attrs = LoadAttributes(fixtureId, clubId, id);
                var player = new MatchPlayerInput(id, Parse<Position>(reader.GetString(2)), attrs, reader.GetInt32(3), reader.GetInt32(4),
                    reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5), reader.GetInt32(6) == 1, reader.GetInt32(7) == 1, reader.GetInt32(8) == 1);
                (starter ? starters : substitutes).Add(player);
            }
        }
        return new MatchTeamInput(clubId, starters, substitutes, new MatchTactics(formation, mentality, passing, pressing, line, width));
    }

    private IReadOnlyDictionary<PlayerAttribute, int> LoadAttributes(string fixtureId, string clubId, string playerId)
    {
        var result = new Dictionary<PlayerAttribute, int>(); using var command = _connection.CreateCommand();
        command.CommandText = "SELECT attribute,value FROM fixture_match_player_attributes WHERE fixture_id=$fixture AND club_id=$club AND player_id=$player ORDER BY attribute;";
        command.Parameters.AddWithValue("$fixture", fixtureId); command.Parameters.AddWithValue("$club", clubId); command.Parameters.AddWithValue("$player", playerId);
        using var reader = command.ExecuteReader(); while (reader.Read()) result.Add(Parse<PlayerAttribute>(reader.GetString(0)), reader.GetInt32(1));
        if (result.Count != Enum.GetValues(typeof(PlayerAttribute)).Length) throw new InvalidOperationException($"Persisted effective attributes are incomplete for player '{playerId}'.");
        return result;
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, false, out var result) && Enum.IsDefined(typeof(T), result) ? result : throw new InvalidOperationException($"Unknown persisted {typeof(T).Name} value '{value}'.");

    private SqliteCommand Create(SqliteTransaction transaction, string sql)
    { var command = _connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command; }
}

/// <summary>Prepared match input and the competition rules used to produce it.</summary>
public sealed class PersistedMatchInput
{
    internal PersistedMatchInput(string competitionId, string competitionRulesJson, MatchInput input)
    { CompetitionId = competitionId; CompetitionRulesJson = competitionRulesJson; Input = input; }
    public string CompetitionId { get; }
    public string CompetitionRulesJson { get; }
    public MatchInput Input { get; }
}
