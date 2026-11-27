using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BT.Core;

/// <summary>Versioned rules for one competition. Runtime behavior must consume this data.</summary>
public sealed class CompetitionRulesConfig
{
    private CompetitionRulesConfig(string id, string version, string format, int teams, bool homeAndAway,
        PointsRules points, IReadOnlyList<string> tiebreakers, PromotionRules movement,
        IReadOnlyList<RegistrationWindow> windows, RegistrationQuotas quotas, DisciplineRules discipline)
    { Id=id; Version=version; Format=format; Teams=teams; HomeAndAway=homeAndAway; Points=points; Tiebreakers=tiebreakers; Movement=movement; Windows=windows; Quotas=quotas; Discipline=discipline; }
    public string Id { get; } public string Version { get; } public string Format { get; }
    public int Teams { get; } public bool HomeAndAway { get; } public PointsRules Points { get; }
    public IReadOnlyList<string> Tiebreakers { get; } public PromotionRules Movement { get; }
    public IReadOnlyList<RegistrationWindow> Windows { get; } public RegistrationQuotas Quotas { get; }
    public DisciplineRules Discipline { get; }

    public static CompetitionRulesConfig Load(string path) => LoadJson(File.ReadAllText(path));
    public static CompetitionRulesConfig LoadJson(string json)
    {
        using var doc=JsonDocument.Parse(json); var root=doc.RootElement;
        foreach(var p in new[]{"id","version","format","teams","homeAndAway","points","tiebreakers","movement","registration","discipline"})
            if(!root.TryGetProperty(p,out _)) throw new InvalidDataException($"Missing competition rule property '{p}'.");
        var id=RequiredString(root,"id"); var version=RequiredString(root,"version"); var format=RequiredString(root,"format");
        if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(version)||string.IsNullOrWhiteSpace(format)) throw new InvalidDataException("id, version and format must be non-empty.");
        var teams=root.GetProperty("teams").GetInt32(); if(teams<2) throw new InvalidDataException("teams must be at least 2.");
        var points=root.GetProperty("points"); var pointRules=new PointsRules(points.GetProperty("win").GetInt32(),points.GetProperty("draw").GetInt32(),points.GetProperty("loss").GetInt32());
        if(pointRules.Win<=pointRules.Draw||pointRules.Draw<=pointRules.Loss||pointRules.Loss<0) throw new InvalidDataException("Points must satisfy win > draw > loss >= 0.");
        var tie=root.GetProperty("tiebreakers").EnumerateArray().Select(x=>x.GetString() ?? throw new InvalidDataException("Tiebreaker cannot be null.")).ToArray();
        if(tie.Length==0||tie.Distinct(StringComparer.Ordinal).Count()!=tie.Length) throw new InvalidDataException("tiebreakers must be non-empty and unique.");
        var move=root.GetProperty("movement"); var movement=new PromotionRules(ReadInt(move,"promotedDirect"),ReadInt(move,"relegatedDirect"),ReadInt(move,"playoffPlaces"));
        var reg=root.GetProperty("registration"); var windows=reg.GetProperty("windows").EnumerateArray().Select(x=>new RegistrationWindow(RequiredString(x,"name"),RequiredString(x,"start"),RequiredString(x,"end"))).ToArray();
        foreach(var w in windows) { if(!DateTime.TryParseExact(w.Start,"MM-dd",null,System.Globalization.DateTimeStyles.None,out _)||!DateTime.TryParseExact(w.End,"MM-dd",null,System.Globalization.DateTimeStyles.None,out _)) throw new InvalidDataException("Window dates must use MM-dd."); }
        var q=reg.GetProperty("quotas"); var quotas=new RegistrationQuotas(ReadInt(q,"seniorMaximum"),ReadInt(q,"eligibleYouthOutsideQuota"),RequiredString(q,"appliesTo"),
            q.TryGetProperty("maximumExtraCommunityRegistered",out var registeredExtra)?registeredExtra.GetInt32():5,
            q.TryGetProperty("maximumExtraCommunityStarters",out var startingExtra)?startingExtra.GetInt32():3);
        if(quotas.SeniorMaximum<0||quotas.EligibleYouthOutsideQuota<0||quotas.MaximumExtraCommunityRegistered<0||quotas.MaximumExtraCommunityStarters<0) throw new InvalidDataException("Quotas cannot be negative.");
        var d=root.GetProperty("discipline"); var yellow=d.GetProperty("yellowAccumulation"); var red=d.GetProperty("directRed");
        var discipline=new DisciplineRules(ReadInt(yellow,"threshold"),ReadInt(yellow,"suspensionMatches"),ReadInt(red,"minimumMatches"),ReadInt(red,"additionalMatches"),RequiredString(d,"yellowScope"),RequiredString(d,"redService"),RequiredString(d,"processingOrder"),
            d.TryGetProperty("accumulationGroupId",out var group)?group.GetString()!:id+":league-yellow",
            d.TryGetProperty("leagueTransferCompetitionId",out var league)?league.GetString()!:"league");
        if(discipline.YellowThreshold<1||discipline.YellowSuspensionMatches<1||discipline.DirectRedMinimum<1||discipline.DirectRedAdditional<0) throw new InvalidDataException("Discipline match thresholds are invalid.");
        if(string.IsNullOrWhiteSpace(discipline.AccumulationGroupId)||string.IsNullOrWhiteSpace(discipline.LeagueTransferCompetitionId)) throw new InvalidDataException("Discipline accumulation and transfer IDs must be non-empty.");
        return new CompetitionRulesConfig(id,version,format,teams,root.GetProperty("homeAndAway").GetBoolean(),pointRules,Array.AsReadOnly(tie),movement,Array.AsReadOnly(windows),quotas,discipline);
    }
    private static string RequiredString(JsonElement e,string p)=>e.GetProperty(p).GetString() ?? throw new InvalidDataException($"{p} cannot be null.");
    private static int ReadInt(JsonElement e,string p)=>e.GetProperty(p).GetInt32();
}
public sealed class PointsRules { public PointsRules(int win,int draw,int loss){Win=win;Draw=draw;Loss=loss;} public int Win{get;} public int Draw{get;} public int Loss{get;} }
public sealed class PromotionRules { public PromotionRules(int promotedDirect,int relegatedDirect,int playoffPlaces){PromotedDirect=promotedDirect;RelegatedDirect=relegatedDirect;PlayoffPlaces=playoffPlaces;} public int PromotedDirect{get;} public int RelegatedDirect{get;} public int PlayoffPlaces{get;} }
public sealed class RegistrationWindow { public RegistrationWindow(string name,string start,string end){Name=name;Start=start;End=end;} public string Name{get;} public string Start{get;} public string End{get;} }
public sealed class RegistrationQuotas { public RegistrationQuotas(int seniorMaximum,int eligibleYouthOutsideQuota,string appliesTo,int maximumExtraCommunityRegistered=5,int maximumExtraCommunityStarters=3){SeniorMaximum=seniorMaximum;EligibleYouthOutsideQuota=eligibleYouthOutsideQuota;AppliesTo=appliesTo;MaximumExtraCommunityRegistered=maximumExtraCommunityRegistered;MaximumExtraCommunityStarters=maximumExtraCommunityStarters;} public int SeniorMaximum{get;} public int EligibleYouthOutsideQuota{get;} public string AppliesTo{get;} public int MaximumExtraCommunityRegistered{get;} public int MaximumExtraCommunityStarters{get;} }
public sealed class DisciplineRules { public DisciplineRules(int yellowThreshold,int yellowSuspensionMatches,int directRedMinimum,int directRedAdditional,string yellowScope,string redService,string processingOrder,string accumulationGroupId="league",string leagueTransferCompetitionId="league"){YellowThreshold=yellowThreshold;YellowSuspensionMatches=yellowSuspensionMatches;DirectRedMinimum=directRedMinimum;DirectRedAdditional=directRedAdditional;YellowScope=yellowScope;RedService=redService;ProcessingOrder=processingOrder;AccumulationGroupId=accumulationGroupId;LeagueTransferCompetitionId=leagueTransferCompetitionId;} public int YellowThreshold{get;} public int YellowSuspensionMatches{get;} public int DirectRedMinimum{get;} public int DirectRedAdditional{get;} public string YellowScope{get;} public string RedService{get;} public string ProcessingOrder{get;} public string AccumulationGroupId{get;} public string LeagueTransferCompetitionId{get;} }
