using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SandplayRelay;

static class HostingChecks
{
    internal static void Run()
    {
        bool rejected=false;
        try { new HostingMeter("http://example.test/",new string('x',32)); }
        catch(ArgumentException) { rejected=true; }
        if(!rejected) throw new Exception("Insecure hosting endpoint accepted");
        var handler=new LeaseHandler();
        using var transport=new HttpClient(handler);
        var meter=new HostingMeter("https://example.test/hosting",new string('x',32),transport);
        using var stopped=new ManualResetEventSlim();
        using(var guard=meter.Start("42",null,"84",()=>stopped.Set()))
        {
            byte[] status=guard.Status();
            if(status.Length!=5 || status[0]!=0 || BitConverter.ToInt32(status,1)>1)
                throw new Exception("Invalid hosting deadline status");
            handler.Fail=true;
            typeof(HostingMeter.Guard).GetMethod("Renew",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(guard,null);
            if(guard.Status()[0]!=1) throw new Exception("Renewal failure missing from status");
            handler.Fail=false;
            if(!stopped.Wait(2000)) throw new Exception("Expired hosting lease did not close room");
        }
        if(handler.Starts!=1 || handler.Ends!=1) throw new Exception("Unexpected hosting requests");
        handler.Fail=true;
        rejected=false;
        try { using var guard=meter.Start("42",null,"84",()=>{}); }
        catch(HttpRequestException) { rejected=true; }
        if(!rejected) throw new Exception("Backend refusal did not reject hosting");
        Console.WriteLine("HOSTING_RELAY_CHECKS_PASSED 5");
    }
    sealed class LeaseHandler : HttpMessageHandler
    {
        internal int Starts, Ends;
        internal bool Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(!request.Headers.Contains("X-Sandtray-Relay-Key")) throw new Exception("Missing service authentication");
            if(Fail) return new HttpResponseMessage(HttpStatusCode.Forbidden);
            using var input=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string action=input.RootElement.GetProperty("action").GetString()!;
            if(input.RootElement.GetProperty("client_user_id").GetInt64()!=84)
                throw new Exception("Missing client session context");
            if(action=="start") Starts++; else if(action=="end") Ends++;
            var now=DateTimeOffset.UtcNow;
            string json=JsonSerializer.Serialize(new {lease_id=input.RootElement.GetProperty("lease_id").GetGuid(),
                server_time=now,deadline=now.AddMilliseconds(150),ended=action=="end"});
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
}
