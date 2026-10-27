using Velqora.Specifications;
int failed=0;
foreach(var spec in Specs.Cases)
{
    try {spec.Value(); Console.WriteLine($"PASS {spec.Key}");}
    catch(Exception ex){failed++; Console.Error.WriteLine($"FAIL {spec.Key}: {ex.Message}");}
}
Console.WriteLine($"{Specs.Cases.Count-failed}/{Specs.Cases.Count} core specifications passed.");
return failed==0?0:1;
