"""Load historical saves using the exact verified Core DLL; never rebuild Core/Game/Tests."""
from pathlib import Path
import argparse,hashlib,json,subprocess,xml.sax.saxutils
R=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(args,log):
 p=subprocess.run(args,cwd=R,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300);log.write_bytes(p.stdout+p.stderr);assert p.returncode==0,log
def main():
 a=argparse.ArgumentParser();a.add_argument('verification');a.add_argument('config');args=a.parse_args();manifest=R/args.verification;config=R/args.config
 v=json.loads(manifest.read_bytes());c=json.loads(config.read_bytes());assert v['status']=='PASSED'
 for f,h in v['binaries'].items():assert sha(R/f)==h,f
 expected=v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll'];files=[];refs={};docs=[]
 for row in c['sources']:
  path=R/row['manifest'];old=json.loads(path.read_bytes());docs.append(path)
  for n in range(row['count']):
   key=next(k for k in old['proofs'] if k.endswith('/'+row['prefix']+'-'+str(n)+'.before.json'));f=R/key;assert sha(f)==old['proofs'][key];files.append(f);refs[key]=sha(f)
 assert len(files)==c['expected_count'] and len(set(files))==len(files)
 P=manifest.parent/'runtime-local/exact-core-legacy-probe';P.mkdir(parents=True,exist_ok=True)
 hint=xml.sax.saxutils.escape((R/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll').as_posix())
 (P/'probe.csproj').write_bytes(('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="HakutaiKagami.Core"><HintPath>'+hint+'</HintPath><Private>true</Private></Reference></ItemGroup></Project>').encode())
 program='''using HakutaiKagami.Core.Contracts;
using System.Security.Cryptography;
using System.Text.Json;
var inputPaths=JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(args.Single()))??throw new Exception("Missing input list");var rows=new List<object>();foreach(string path in inputPaths){var raw=File.ReadAllBytes(path);var session=new WorldSession(ContractJson.Read<ContractWorld>(raw));var saved=ContractJson.Write(session.DiagnosticSnapshot());if(!raw.SequenceEqual(saved))throw new Exception("Saved world bytes changed: "+path);rows.Add(new{Path=path,Sha256=Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant()});}
string core=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WorldSession).Assembly.Location))).ToLowerInvariant();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"../../..","result.json"),JsonSerializer.Serialize(new{Status="PASSED",Files=inputPaths.Length,CoreSha256=core,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS saved world bytes: "+inputPaths.Length);
'''
 (P/'Program.cs').write_bytes(program.encode())
 run(['dotnet','build',str(P/'probe.csproj'),'--nologo','--verbosity','quiet'],P/'build.log')
 for f,h in v['binaries'].items():assert sha(R/f)==h,'Verified binary changed: '+f
 copied=P/'bin/Debug/net8.0/HakutaiKagami.Core.dll';assert sha(copied)==expected,'Probe loaded a different Core'
 (P/'inputs.json').write_bytes(json.dumps([str(f) for f in files]).encode())
 run(['dotnet',str(P/'bin/Debug/net8.0/probe.dll'),str(P/'inputs.json')],P/'run.log')
 result=json.loads((P/'result.json').read_bytes());assert result['Status']=='PASSED' and result['Files']==c['expected_count'] and result['CoreSha256']==expected
 for row in result['Rows']:assert sha(Path(row['Path']))==row['Sha256']==refs[Path(row['Path']).relative_to(R).as_posix()]
 v['proofs'].update(refs)
 for f in [Path(__file__),config,*docs,P/'probe.csproj',P/'Program.cs',P/'inputs.json',P/'build.log',P/'run.log',P/'result.json',P/'bin/Debug/net8.0/probe.dll',copied]:v['proofs'][f.relative_to(R).as_posix()]=sha(f)
 v['legacy_probe']=dict(status='PASSED',files=len(files),result=(P/'result.json').relative_to(R).as_posix(),core_sha256=expected,scope='Actual old files loaded and rewritten byte-identically using a direct reference to the exact verified Core DLL; production binaries were not rebuilt; no GUI')
 manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());print('PASS exact verified Core / old saved worlds '+str(len(files)))
if __name__=='__main__':main()
