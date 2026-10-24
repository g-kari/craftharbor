using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

partial class CraftHarbor
{
    Dictionary<int,string> discoveredVoiceMap=new Dictionary<int,string>();
    static List<VoiceEndpoint> VoiceEndpoints(string server,int gamePort)
    {
        var result=new List<VoiceEndpoint>();var files=new List<string>();foreach(string dir in new[]{"mods","plugins"}){string path=Path.Combine(server,dir);if(Directory.Exists(path))files.AddRange(Directory.GetFiles(path,"*.jar").Select(p=>Regex.Replace(Path.GetFileName(p).ToLowerInvariant(),"[^a-z0-9]","")));}
        if(files.Any(n=>n.Contains("voicechat")))foreach(string relative in new[]{"config/voicechat/voicechat-server.properties","plugins/voicechat/voicechat-server.properties","plugins/SimpleVoiceChat/voicechat-server.properties"}){string path=Path.Combine(server,relative);if(!File.Exists(path))continue;var m=Regex.Match(File.ReadAllText(path),@"(?m)^\s*port\s*=\s*(-?\d+)(?:\s*#.*)?\s*$");int port=24454;if(m.Success&&!int.TryParse(m.Groups[1].Value,out port))continue;if(port==-1)port=gamePort;if(port>0&&port<=65535)result.Add(new VoiceEndpoint{Name="Simple Voice Chat",Config=path,Port=port});}
        if(files.Any(n=>n.Contains("plasmovoice")))foreach(string relative in new[]{"config/plasmovoice/server/config.toml","config/plasmovoice/server.toml","config/plasmovoice/config.toml","plugins/PlasmoVoice/config.toml"}){string path=Path.Combine(server,relative);if(!File.Exists(path))continue;string host=Regex.Match(File.ReadAllText(path),@"(?ms)^\s*\[host\][^\r\n]*\r?\n(.*?)(?=^\s*\[|\z)").Groups[1].Value;var m=Regex.Match(host,@"(?m)^\s*port\s*=\s*(\d+)(?:\s*#.*)?\s*$");int port=0;if(m.Success&&!int.TryParse(m.Groups[1].Value,out port))continue;if(port==0)port=gamePort;if(port>0&&port<=65535)result.Add(new VoiceEndpoint{Name="Plasmo Voice",Config=path,Port=port});}
        return result;
    }
    static bool VoiceAddressValid(string address){Uri u;return ValidPublicEndpoint(address)&&Uri.TryCreate("udp://"+address,UriKind.Absolute,out u)&&u.Port>0&&u.Port<=65535;}
    static bool WriteVoiceAddress(VoiceEndpoint endpoint,string address)
    {
        if(!VoiceAddressValid(address)||!File.Exists(endpoint.Config))return false;string original=File.ReadAllText(endpoint.Config),next;
        if(endpoint.Name=="Simple Voice Chat")next=Regex.Replace(original,@"(?m)^\s*voice_host=.*(?:\r?\n|$)","").TrimEnd()+"\r\nvoice_host="+address+"\r\n";
        else{var uri=new Uri("udp://"+address);var pattern=new Regex(@"(?ms)^\s*\[host\.public\][^\r\n]*\r?\n(.*?)(?=^\s*\[|\z)");var match=pattern.Match(original);string body=match.Success?Regex.Replace(match.Groups[1].Value,@"(?m)^\s*(?:ip|port)\s*=.*(?:\r?\n|$)",""):"";string section="[host.public]\r\n"+(body.Trim().Length>0?body.TrimEnd()+"\r\n":"")+"ip = \""+uri.Host+"\"\r\nport = "+uri.Port+"\r\n";next=match.Success?pattern.Replace(original,section,1):original.TrimEnd()+"\r\n\r\n"+section;}
        if(next.Replace("\r\n","\n")==original.Replace("\r\n","\n"))return false;string backup=endpoint.Config+".before-craftharbor.bak";if(!File.Exists(backup))File.Copy(endpoint.Config,backup,false);string temp=endpoint.Config+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,next,new UTF8Encoding(false));File.Replace(temp,endpoint.Config,null);return true;
    }
    void ApplyVoiceAutomatically()
    {
        if(!hosting.AutoVoice||Running(server))return;if(hosting.VoiceAddresses==null)hosting.VoiceAddresses=new Dictionary<string,string>();var endpoints=DetectVoice();foreach(var item in endpoints){string address;if(!hosting.VoiceAddresses.TryGetValue(item.Port.ToString(),out address)){if(endpoints.Select(v=>v.Port).Distinct().Count()!=1)continue;address=hosting.VoiceAddress;}if(WriteVoiceAddress(item,address))Add("[音声] "+item.Name+"のUDP "+item.Port+"に対応する公開先を自動反映しました。");}
    }
}
