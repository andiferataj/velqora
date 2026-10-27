const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const root=path.resolve(__dirname,'..','src/frontend/velqora-dashboard');
const localRequire=createRequire(path.join(root,'package.json'));
const ts=process.env.VELQORA_TYPESCRIPT_PATH?require(process.env.VELQORA_TYPESCRIPT_PATH):localRequire('typescript');
let checked=0,errors=0;
function scan(dir){
  for(const item of fs.readdirSync(dir,{withFileTypes:true})){
    const file=path.join(dir,item.name);
    if(item.isDirectory())scan(file);
    else if(file.endsWith('.ts')){
      const source=ts.createSourceFile(file,fs.readFileSync(file,'utf8'),ts.ScriptTarget.Latest,true);
      for(const d of source.parseDiagnostics){console.error(file,ts.flattenDiagnosticMessageText(d.messageText,'\n'));errors++;}
      checked++;
    }
  }
}
scan(path.join(root,'src'));
console.log('TypeScript syntax: '+checked+' files; '+errors+' errors. Angular template/type checking requires npm run build.');
process.exitCode=errors?1:0;
