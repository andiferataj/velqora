// Local verification server for a production build with the deployed security policy.
const http=require('node:http'),fs=require('node:fs/promises'),path=require('node:path');
const build=path.resolve(__dirname,'../.local/dashboard-build/browser');
const target=new URL(process.env.VELQORA_PREVIEW_API||'http://127.0.0.1:18080');
const port=Number(process.env.VELQORA_PREVIEW_PORT||14200);
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.json':'application/json'};
(async()=>{
 const config=await fs.readFile(path.resolve(__dirname,'../src/frontend/velqora-dashboard/nginx.conf'),'utf8');
 const policy=config.match(/add_header Content-Security-Policy "([^"]+)"/)[1];
 const server=http.createServer(async(req,res)=>{
  res.setHeader('Content-Security-Policy',policy);
  res.setHeader('X-Content-Type-Options','nosniff');
  res.setHeader('Cache-Control','no-store');
  if(req.url.startsWith('/api/')||req.url.startsWith('/hubs/')){
   const upstream=http.request({hostname:target.hostname,port:target.port,path:req.url,method:req.method,headers:{...req.headers,host:target.host}},response=>{
    res.writeHead(response.statusCode,response.headers);response.pipe(res);
   });
   upstream.on('error',()=>{res.writeHead(502);res.end('Verification API unavailable');});req.pipe(upstream);return;
  }
  try{
   const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
   let file=path.resolve(build,'.'+pathname);
   if(!file.startsWith(build+path.sep)&&file!==build){res.writeHead(403);res.end();return;}
   if(pathname==='/'||!path.extname(pathname))file=path.join(build,'index.html');
   const data=await fs.readFile(file);res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.end(data);
  }catch{res.writeHead(404);res.end();}
 });
 server.on('upgrade',(req,socket,head)=>{
  const upstream=http.request({hostname:target.hostname,port:target.port,path:req.url,headers:{...req.headers,host:target.host}});
  upstream.on('upgrade',(response,remote,remoteHead)=>{
   socket.write('HTTP/1.1 101 Switching Protocols\r\n'+Object.entries(response.headers).map(([k,v])=>k+': '+v).join('\r\n')+'\r\n\r\n');
   if(remoteHead.length)socket.write(remoteHead);if(head.length)remote.write(head);
   socket.pipe(remote).pipe(socket);remote.on('error',()=>socket.destroy());socket.on('error',()=>remote.destroy());
  });
  upstream.on('error',()=>socket.destroy());upstream.end();
 });
 server.listen(port,'127.0.0.1',()=>console.log('Verification preview: http://127.0.0.1:'+port));
 process.on('SIGINT',()=>server.close(()=>process.exit(0)));
})();
