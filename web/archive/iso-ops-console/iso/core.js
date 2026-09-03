'use strict';
/* MechaTrader isometric stage — K3 engine core (palette, tilt, two canvases).
 * Combat, audio and the battle loop stay in k3-game-demo. This file only
 * owns the surfaces the volume renderer draws into. */

const C={bg:'#0b0d11',panel:'#14181f','panel-2':'#1b212a','panel-3':'#232b36',
  line:'#2c3542','line-hi':'#3d4959',ink:'#e7eaef','ink-2':'#a3adbb','ink-3':'#6d7887','ink-4':'#48525f',
  amber:'#d9a13c',rust:'#c05f3c',azure:'#4b83c2',steel:'#6b8296',rose:'#bd5b78',
  moss:'#6f8f52',jade:'#4fa08a',violet:'#8f6bb5'};

const TAU=Math.PI*2, PI=Math.PI;
const rnd=(a,b)=>a+Math.random()*(b-a);
const clamp=(v,a,b)=>v<a?a:v>b?b:v;
const hsh=(i,j)=>{const v=Math.sin(i*127.1+j*311.7)*43758.5453;return v-Math.floor(v);};
function vnoise(x,y,s){
  const i=Math.floor(x),j=Math.floor(y),fx=x-i,fy=y-j;
  const u=fx*fx*(3-2*fx), v=fy*fy*(3-2*fy);
  const a=hsh(i+s*7.3,j+s*3.1),b=hsh(i+1+s*7.3,j+s*3.1);
  const c=hsh(i+s*7.3,j+1+s*3.1),d=hsh(i+1+s*7.3,j+1+s*3.1);
  return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v;
}
function rngOf(seed){let s=(seed>>>0)||1;return()=>{s^=s<<13;s>>>=0;s^=s>>>17;s^=s<<5;s>>>=0;return s/4294967296;};}

function px(h){h=h.replace('#','');return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];}
const RGB={};for(const k in C)RGB[k]=px(C[k]);
const A_INK=RGB.ink, A_BG=RGB.bg;
function shade(rgb,t){
  t=clamp(t,-1,1);const a=t>=0?A_INK:A_BG,k=t>=0?t:-t;
  return 'rgb('+((rgb[0]+(a[0]-rgb[0])*k)|0)+','+((rgb[1]+(a[1]-rgb[1])*k)|0)+','+((rgb[2]+(a[2]-rgb[2])*k)|0)+')';
}
function col(k){
  if(Array.isArray(k))return k;
  if(k==null)return RGB['panel-2'];
  if(RGB[k])return RGB[k];
  const s=String(k).replace(/^a-/,'');
  if(RGB[s])return RGB[s];
  return s.charAt(0)==='#'?px(s):RGB['panel-2'];
}

const TILT_3D=.55, TILT_TOP=.90;
let tilt=TILT_3D, tiltT=TILT_3D;
const tiltQ=()=>Math.round(tilt*20);

const PXLV=[1,2,3,4];
let pxI=1, PXS=PXLV[pxI];
const cv=document.getElementById('cv');
const scr=cv.getContext('2d');
const wb=document.getElementById('wcv');
const wctx=wb.getContext('2d',{alpha:false});
let ctx=wctx;

let DPRCAP=1.5;
const ZLV=[.78,1.05,1.38];
let zI=1, ZOOM=ZLV[zI];
let W=900,H=600,SCW=900,SCH=600,dpr=1,vig=null;
let BS=ZOOM/PXS;
let LW=1;
function fit(){
  const r=cv.getBoundingClientRect();
  SCW=Math.max(2,Math.round(r.width));SCH=Math.max(2,Math.round(r.height));
  W=SCW/ZOOM;H=SCH/ZOOM;
  dpr=Math.min(DPRCAP,window.devicePixelRatio||1);
  cv.width=Math.round(SCW*dpr);cv.height=Math.round(SCH*dpr);
  scr.setTransform(dpr,0,0,dpr,0,0);
  scr.imageSmoothingEnabled=false;
  BS=ZOOM/PXS;
  LW=Math.max(1,1/BS);
  wb.width=Math.max(1,Math.ceil(SCW/PXS));wb.height=Math.max(1,Math.ceil(SCH/PXS));
  wb.style.width=(wb.width*PXS)+'px';wb.style.height=(wb.height*PXS)+'px';
  wctx.setTransform(BS,0,0,BS,0,0);
  wctx.imageSmoothingEnabled=false;
  vig=wctx.createRadialGradient(W/2,H/2,Math.min(W,H)*.42,W/2,H/2,Math.max(W,H)*.74);
  vig.addColorStop(0,'rgba(0,0,0,0)');vig.addColorStop(1,'rgba(0,0,0,.42)');
}
const snap=v=>Math.round(v*BS)/BS;
let blitMode='draw';
function dropBakes(){
  if(typeof sprites!=='undefined')sprites.clear();
  if(typeof chunks!=='undefined')chunks.clear();
}
function setPXS(i){pxI=(i+PXLV.length)%PXLV.length;PXS=PXLV[pxI];dropBakes();fit();}
function setZoom(i){zI=clamp(i,0,ZLV.length-1)|0;ZOOM=ZLV[zI];dropBakes();fit();}
addEventListener('resize',fit);

/* The city grammar from k3 — URBAN RUIN floor + dashed grid is the look. */
const BIO={
  id:'overland',name:'OVERLAND',cat:'earth',ground:'soil',gtone:'moss',
  p1:'moss',p2:'panel-3',p3:'panel-2',lit:'amber',base:'#0a0c10',
  props:['tree','pine','bush','rock','boulder','log','dead','rubble','crate'],
  grid:null
};

function variantR(spec,vi){return spec.r[0]+(spec.r[1]-spec.r[0])*((vi+.5)/spec.vari);}
function variantVols(spec,vi,r,stage){
  return spec.build(rngOf(spec.salt*7919+vi*104729+2166136261),r,BIO);
}

let PATH=[];
let MPROP=[];
const MHB=256;
function emit(spec,x,y,vi,sc,tag){
  MPROP.push({spec:spec,x:x,y:y,vi:vi,sc:sc,r:variantR(spec,vi)*sc,
              key:spec.id+'|'+MPROP.length,tag:tag||''});
}
function buildHash(){
  for(const id in SPECIES)SPECIES[id]._hash=null;
  for(const p of MPROP){
    const s=p.spec;if(!s._hash)s._hash=new Map();
    const k=(Math.floor(p.x/MHB)+512)*1024+(Math.floor(p.y/MHB)+512);
    let a=s._hash.get(k);if(!a){a=[];s._hash.set(k,a);}a.push(p);
  }
}
function scatterRect(spec,x0,y0,x1,y1,cb){
  const H=spec._hash;if(!H)return;
  const pad=spec.r[1]*1.6;
  const bx0=Math.floor((x0-pad)/MHB),bx1=Math.floor((x1+pad)/MHB);
  const by0=Math.floor((y0-pad)/MHB),by1=Math.floor((y1+pad)/MHB);
  for(let bx=bx0;bx<=bx1;bx++)for(let by=by0;by<=by1;by++){
    const a=H.get((bx+512)*1024+(by+512));if(!a)continue;
    for(let i=0;i<a.length;i++){
      const p=a[i];
      if(p.x<x0-pad||p.x>x1+pad||p.y<y0-pad||p.y>y1+pad)continue;
      cb(p.x,p.y,p.r,p.vi,p.sc,p.key);
    }
  }
}
const VIS=[];
function drawInstance(it,t){
  const spec=it.spec, tq=tiltQ();
  const s=getSprite(spec,it.vi,tq,0);
  if(s){
    const k=it.sc;
    ctx.drawImage(s.c, it.x+s.ox*k, it.y*t+s.oy*k, s.w*k, s.h*k);
  }else{
    ctx.save();ctx.translate(it.x,it.y*t);ctx.scale(it.sc,it.sc);
    const vols=variantVols(spec,it.vi,variantR(spec,it.vi),0);
    vols.sort(volSort);
    for(let i=0;i<vols.length;i++)drawVol(ctx,vols[i],t);
    ctx.restore();
  }
}
function gather(cx,cy,t){
  VIS.length=0;
  const halfW=W/2+180, halfH=H/2/t+340;
  const x0=cx-halfW,x1=cx+halfW,y0=cy-halfH,y1=cy+halfH;
  for(const id in SPECIES){
    const spec=SPECIES[id];
    if(!spec||spec.flat)continue;
    scatterRect(spec,x0,y0,x1,y1,(x,y,r,vi,sc,k)=>{
      VIS.push({x:x,y:y,sy:y+r*.5,r:r,vi:vi,sc:sc,spec:spec,key:k});
    });
  }
  VIS.sort((a,b)=>a.sy-b.sy);
}
