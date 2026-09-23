"""Exercise relay membership filtering and request races without a live API."""
import pathlib, subprocess, tempfile
root=pathlib.Path(__file__).resolve().parent
harness=r'''
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
namespace SandplayRelay;
class ProfileTestHandler:HttpMessageHandler {
 public int Calls; public string Assertion="";
 public TaskCompletionSource<bool> Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel) {
  Calls++;Assertion=string.Join("",request.Headers.GetValues("X-Relay-Profiles"));await Gate.Task;
  return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"profiles\":[]}")};
 }
}
class ProfileTests {
 static async Task Main() {
  string key="test-only-relay-signing-key-32-bytes-minimum";
  Environment.SetEnvironmentVariable("RELAY_TICKET_SIGNING_KEY",key);
  typeof(Program).GetField("tickets",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,new RelayTickets(key));
  var call=typeof(Program).GetMethod("RequestProfiles",BindingFlags.NonPublic|BindingFlags.Static)!;
  using var host=new TcpClient();using var member=new TcpClient();using var outsider=new TcpClient();
  var room=new Room {Code="ABC123",Host=host,HostAccount="1"};room.Clients.Add(member);room.Accounts[member]="2";room.Tokens[member]="member";
  using var handler=new ProfileTestHandler();using var http=new HttpClient(handler);using var output=new MemoryStream();
  void Request(TcpClient who, byte[] payload) => call.Invoke(null,new object[]{room,who,output,payload,http});
  Request(outsider,Array.Empty<byte>());Request(host,new byte[]{1});
  await Task.Delay(50);if(handler.Calls!=0) throw new Exception("Unauthorized request reached API");
  Request(host,Array.Empty<byte>());
  for(int n=0;n<100 && handler.Calls==0;n++) await Task.Delay(10);
  string body=handler.Assertion.Split('.')[1].Replace('-','+').Replace('_','/');body=body.PadRight((body.Length+3)/4*4,'=');
  using var claims=JsonDocument.Parse(Convert.FromBase64String(body));
  if(claims.RootElement.GetProperty("members").GetArrayLength()!=2) throw new Exception("Wrong members");
  if(claims.RootElement.GetProperty("requester").GetString()!="1") throw new Exception("Requester identity missing");
  Request(host,Array.Empty<byte>());if(handler.Calls!=1) throw new Exception("Duplicate request not blocked");
  handler.Gate.SetResult(true);
  for(int n=0;n<100;n++){lock(room.Lock){if(!room.ProfilePending.Contains(host))break;}await Task.Delay(10);}
  if(output.Length==0 || output.ToArray()[4]!=41)throw new Exception("Missing relay response");
  using var delayed=new ProfileTestHandler();using var delayedHttp=new HttpClient(delayed);using var discarded=new MemoryStream();
  room.ProfileRequests.Clear();call.Invoke(null,new object[]{room,host,discarded,Array.Empty<byte>(),delayedHttp});
  for(int n=0;n<100 && delayed.Calls==0;n++)await Task.Delay(10);
  lock(room.Lock)room.Clients.Remove(member);
  delayed.Gate.SetResult(true);
  for(int n=0;n<100;n++){lock(room.Lock){if(!room.ProfilePending.Contains(host))break;}await Task.Delay(10);}
  if(discarded.Length!=0)throw new Exception("Stale membership disclosed");
  Console.WriteLine("RELAY_SESSION_PROFILE_CHECKS_PASSED");
 }
}
'''
with tempfile.TemporaryDirectory(prefix='relay-profiles-') as folder:
 p=pathlib.Path(folder)
 for name in ('Program.cs','RelayTickets.cs','SessionProfiles.cs','HostingMeter.cs'): (p/name).write_text((root/name).read_text())
 (p/'Tests.cs').write_text(harness)
 (p/'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><StartupObject>SandplayRelay.ProfileTests</StartupObject></PropertyGroup></Project>')
 subprocess.run(['dotnet','run','--project',str(p/'Checks.csproj')],check=True,timeout=90)
