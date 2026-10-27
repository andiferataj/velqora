using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Velqora.Core;
var root=args.Length>0?Path.GetFullPath(args[0]):Directory.GetCurrentDirectory();
var directories=new[]{"src/backend","tests","tools/SourceChecks"};
var files=directories.SelectMany(d=>Directory.EnumerateFiles(Path.Combine(root,d),"*.cs",SearchOption.AllDirectories))
    .Where(p=>!p.Split(Path.DirectorySeparatorChar).Any(s=>s is "obj" or "bin")).ToArray();
var coreTypes=typeof(Entity).Assembly.GetTypes().Where(t=>t.IsClass&&!t.IsAbstract&&!t.IsNested&&t.Namespace=="Velqora.Core").ToDictionary(t=>t.Name);
int failed=0;
foreach(var file in files)
{
    var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.Latest),file);
    foreach(var diagnostic in tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error))
    {Console.Error.WriteLine(diagnostic);failed++;}
    foreach(var creation in tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
    {
        if(!coreTypes.TryGetValue(creation.Type.ToString(),out var type)||creation.Initializer==null)continue;
        foreach(var assignment in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            var member=assignment.Left.ToString();
            if(type.GetProperty(member)==null){Console.Error.WriteLine($"{file}: {type.Name}.{member} is not a domain property");failed++;}
        }
    }
}
var tables=new Dictionary<string,string>{
    [nameof(SecurityEvent)]="SecurityEvents",[nameof(OutboxMessage)]="OutboxMessages",[nameof(ProcessedMessage)]="ProcessedMessages",
    [nameof(DetectionRule)]="DetectionRules",[nameof(Alert)]="Alerts",[nameof(AlertEvidence)]="AlertEvidence",
    [nameof(Incident)]="Incidents",[nameof(IncidentAlert)]="IncidentAlerts",[nameof(TimelineEntry)]="IncidentTimeline",
    [nameof(Asset)]="Assets",[nameof(Playbook)]="Playbooks",[nameof(PlaybookRun)]="PlaybookRuns",
    [nameof(ScenarioRun)]="ScenarioRuns",[nameof(AuditEvent)]="AuditEvents",[nameof(ServiceHeartbeat)]="ServiceHeartbeats"
};
var sql=File.ReadAllText(Path.Combine(root,"src/backend/Velqora.Infrastructure/Migrations/initial.sql"));
foreach(var (entity,table) in tables)
{
    var match=Regex.Match(sql,"CREATE TABLE \""+table+"\" \\(([\\s\\S]*?)\\);");
    if(!match.Success){Console.Error.WriteLine($"Migration missing table {table}");failed++;continue;}
    var columns=Regex.Matches(match.Groups[1].Value,"\"(\\w+)\"\\s+(?:uuid|integer|timestamp with time zone|timestamptz|boolean|character varying|varchar|jsonb)")
        .Select(m=>m.Groups[1].Value).ToHashSet();
    var properties=coreTypes[entity].GetProperties(BindingFlags.Public|BindingFlags.Instance).Select(p=>p.Name).ToHashSet();
    foreach(var missing in properties.Except(columns)){Console.Error.WriteLine($"Migration missing {table}.{missing}");failed++;}
    foreach(var extra in columns.Except(properties)){Console.Error.WriteLine($"Migration has unknown {table}.{extra}");failed++;}
}
Console.WriteLine($"Source checks: {files.Length} C# files, {tables.Count} domain table/column comparisons; {failed} errors.");
Console.WriteLine("These checks do not replace EF migration execution or dependency type checking.");
return failed==0?0:1;
