import {createApi,createStore,securityHeaders} from './api.mjs';
export default {
 async fetch(request,env){
  const url=new URL(request.url);
  if(url.pathname.startsWith('/api/')){
   if(!env.DB)return new Response(JSON.stringify({error:'Archive database is not configured'}),{status:503,headers:{...securityHeaders,'Content-Type':'application/json'}});
   return createApi(createStore(env.DB),{
    enabled:env.UPLOADS_ENABLED==='true',
    limitUpload:async r=>{
     if(!env.UPLOAD_LIMITER)return false;
     const ip=r.headers.get('CF-Connecting-IP');if(!ip)return false;
     return (await env.UPLOAD_LIMITER.limit({key:ip})).success;
    }
   })(request);
  }
  if(url.pathname==='/logs')return Response.redirect(new URL('/logs/',url),302);
  if(!url.pathname.startsWith('/logs/'))return new Response('Not found',{status:404,headers:securityHeaders});
  const response=await env.ASSETS.fetch(request),headers=new Headers(response.headers);
  for(const [k,v]of Object.entries(securityHeaders))headers.set(k,v);
  return new Response(response.body,{status:response.status,headers});
 }
};
