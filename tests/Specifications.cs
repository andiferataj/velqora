using System.Text.Json;
using Velqora.Core;
namespace Velqora.Specifications;

public static class Specs
{
    private static readonly DateTimeOffset Now = new(2026,10,26,12,0,0,TimeSpan.Zero);
    private static SecurityEvent E(string type="auth.failure",int seconds=0,string device="WS",string account="andi",Guid? run=null) =>
        new(){Type=type,OccurredAt=Now.AddSeconds(seconds),Device=device,Account=account,Synthetic=true,ScenarioRunId=run};
    private static DetectionRule R(RuleKind kind=RuleKind.Threshold)=>new(){Kind=kind,Threshold=10,WindowSeconds=300};
    private static EventInput Input()=>new(Guid.NewGuid(),1,Now,"test","auth.failure","WS","andi","192.0.2.10",new(){{"a","one"}},true);
    private static void Assert(bool value,string why){if(!value)throw new Exception(why);}
    private static void Reject(Action action,int status=400){try{action();}catch(DomainException ex){Assert(ex.Status==status,"Wrong status");return;}throw new Exception("Expected validation rejection");}

    public static readonly IReadOnlyDictionary<string,Action> Cases = new Dictionary<string,Action>
    {
        ["threshold below boundary"] = () => {var e=Enumerable.Range(0,9).Select(i=>E(seconds:i)).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Nine failures must not alert");},
        ["threshold at boundary"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(seconds:i)).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)?.Evidence.Count==10,"Ten failures must alert");},
        ["window excludes stale events"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(seconds:i*40)).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Stale failures counted");},
        ["window includes exact lower bound"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(seconds:i==0?-300:0)).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)!=null,"Boundary is inclusive");},
        ["device isolation"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(device:i==0?"OTHER":"WS")).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Different devices mixed");},
        ["identity isolation"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(account:i==0?"other":"andi")).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Different identities mixed");},
        ["synthetic data isolation"] = () => {var e=Enumerable.Range(0,10).Select(_=>E()).ToArray();e[0].Synthetic=false;Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Synthetic and real data mixed");},
        ["scenario isolation"] = () => {var e=Enumerable.Range(0,10).Select(_=>E(run:Guid.NewGuid())).ToArray();Assert(DetectionEngine.Evaluate(R(),e[^1],e)==null,"Different runs mixed");},
        ["duplicate evidence IDs"] = () => {var e=E();var copies=Enumerable.Repeat(e,10).ToArray();Assert(DetectionEngine.Evaluate(R(),e,copies)==null,"Duplicate IDs counted as independent events");},
        ["future events excluded"] = () => {var e=Enumerable.Range(0,10).Select(i=>E(seconds:i)).ToArray();Assert(DetectionEngine.Evaluate(R(),e[0],e)==null,"Future events counted");},
        ["disabled rule"] = () => {var r=R();r.Enabled=false;var e=Enumerable.Range(0,10).Select(_=>E()).ToArray();Assert(DetectionEngine.Evaluate(r,e[^1],e)==null,"Disabled rule ran");},
        ["success after failures"] = () => {var e=Enumerable.Range(0,12).Select(i=>E(seconds:i)).Append(E("auth.success",13)).ToArray();Assert(DetectionEngine.Evaluate(R(RuleKind.FailureThenSuccess),e[^1],e)?.Evidence.Count==13,"Sequence detection failed");},
        ["success requires prior failures"] = () => {var e=E("auth.success");Assert(DetectionEngine.Evaluate(R(RuleKind.FailureThenSuccess),e,[e])==null,"Benign success alerted");},
        ["failures after success excluded"] = () => {var e=Enumerable.Range(1,12).Select(i=>E(seconds:i)).Append(E("auth.success")).ToArray();Assert(DetectionEngine.Evaluate(R(RuleKind.FailureThenSuccess),e[^1],e)==null,"Wrong sequence order");},
        ["privilege change"] = () => {var e=E("privilege.change");Assert(DetectionEngine.Evaluate(R(RuleKind.PrivilegeMisuse),e,[e])!=null,"Privilege misuse missing");},
        ["normal login not privilege change"] = () => {var e=E("auth.success");Assert(DetectionEngine.Evaluate(R(RuleKind.PrivilegeMisuse),e,[e])==null,"Benign login misclassified");},
        ["event contract round trip"] = () => {var input=Input();var copy=JsonSerializer.Deserialize<EventInput>(JsonSerializer.Serialize(input))!;Assert(Guard.Event(input,Now).PayloadHash==Guard.Event(copy,Now).PayloadHash,"Contract changed in transport");},
        ["canonical attribute ordering"] = () => {var i=Input() with{Attributes=new(){{"b","two"},{"a","one"}}};var j=i with{Attributes=new(){{"a","one"},{"b","two"}}};Assert(Guard.Event(i,Now).PayloadHash==Guard.Event(j,Now).PayloadHash,"Attribute order changed hash");},
        ["different payload changes hash"] = () => {var i=Input();Assert(Guard.Event(i,Now).PayloadHash!=Guard.Event(i with{Account="other"},Now).PayloadHash,"Conflicting content not identifiable");},
        ["invalid schema rejected"] = () => Reject(()=>Guard.Event(Input() with{SchemaVersion=2},Now)),
        ["empty ID rejected"] = () => Reject(()=>Guard.Event(Input() with{Id=Guid.Empty},Now)),
        ["future timestamp rejected"] = () => Reject(()=>Guard.Event(Input() with{OccurredAt=Now.AddMinutes(3)},Now)),
        ["expired timestamp rejected"] = () => Reject(()=>Guard.Event(Input() with{OccurredAt=Now.AddDays(-8)},Now)),
        ["invalid IP rejected"] = () => Reject(()=>Guard.Event(Input() with{IpAddress="not-an-ip"},Now)),
        ["oversized attributes rejected"] = () => Reject(()=>Guard.Event(Input() with{Attributes=new(){{"x",new string('x',513)}}},Now)),
        ["rule bounds validated"] = () => Reject(()=>Guard.Rule(new("Rule","",RuleKind.Threshold,"auth.failure",0,300,Severity.High,true))),
        ["unknown enum rejected"] = () => Reject(()=>Guard.EnumValue((Severity)99)),
        ["CSV formulas neutralized"] = () => {foreach(var text in new[]{"=HYPERLINK(1)","+cmd","-1","@SUM(1)","  =x","\tformula"})Assert(Guard.Csv(text).StartsWith("\"'"),"CSV formula not escaped");},
        ["CSV quotes escaped"] = () => Assert(Guard.Csv("a\"b")=="\"a\"\"b\"","CSV quoting incorrect"),
        ["incident state transitions"] = () => {Assert(!DetectionEngine.CanTransition(IncidentStatus.Open,IncidentStatus.Closed),"Case skipped investigation");Assert(DetectionEngine.CanTransition(IncidentStatus.Investigating,IncidentStatus.Contained),"Containment blocked");Assert(DetectionEngine.CanTransition(IncidentStatus.Closed,IncidentStatus.Investigating),"Reopen blocked");},
        ["deterministic replay IDs"] = () => {var r=new ScenarioRun{CreatedAt=Now,Device="LAB",Account="lab"};Assert(Scenarios.Event(r,0).Id==Scenarios.Event(r,0).Id,"Retry ID unstable");Assert(Scenarios.Event(r,0).Id!=Scenarios.Event(r,1).Id,"Sequence IDs collide");},
        ["replay run IDs isolated"] = () => {var a=new ScenarioRun{CreatedAt=Now};var b=new ScenarioRun{CreatedAt=Now};Assert(Scenarios.Event(a,0).Id!=Scenarios.Event(b,0).Id,"Replay duplicated prior events");},
        ["central scenario produces detections"] = () => {var r=new ScenarioRun{CreatedAt=Now,Device="LAB",Account="lab"};var e=Enumerable.Range(0,13).Select(i=>Guard.Event(Scenarios.Event(r,i),Now.AddMinutes(1))).ToArray();Assert(e.Count(x=>x.Type=="auth.failure")==12,"Wrong failed login count");Assert(DetectionEngine.Evaluate(R(),e[11],e)!=null,"Threshold missing");Assert(DetectionEngine.Evaluate(R(RuleKind.FailureThenSuccess),e[12],e)!=null,"Sequence missing");},
        ["benign baseline has no login alert"] = () => {var r=new ScenarioRun{CreatedAt=Now,Device="LAB",Account="lab",Scenario="benign-baseline"};var e=Enumerable.Range(0,30).Select(i=>Guard.Event(Scenarios.Event(r,i),Now.AddMinutes(1))).ToArray();Assert(e.All(x=>DetectionEngine.Evaluate(R(),x,e)==null&&DetectionEngine.Evaluate(R(RuleKind.FailureThenSuccess),x,e)==null),"Benign baseline generated false positives");},
        ["core architecture has no infrastructure dependencies"] = () => Assert(typeof(SecurityEvent).Assembly.GetReferencedAssemblies().All(x=>!x.Name!.Contains("EntityFramework")&&!x.Name.Contains("RabbitMQ")&&!x.Name.Contains("Velqora.Infrastructure")),"Core coupled to infrastructure")
    };
}
