using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    public static class VFX
    {
        public static RectTransform Layer { get; private set; }
        public static Font Font { get; set; }
        public static RectTransform ShakeRoot { get; set; }
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly List<Particle> active=new List<Particle>(256);
        static readonly Stack<Particle> images=new Stack<Particle>(256), texts=new Stack<Particle>(32);
        static readonly Color[] warm={new Color(1,.84f,.42f),Color.white};
        static readonly Color[] party={new Color(.95f,.55f,.48f),new Color(.65f,.83f,.4f),new Color(.49f,.78f,.91f),new Color(1,.84f,.42f),new Color(.49f,.84f,.78f)};
        static float rainRemaining,rainRate,rainAccumulator;
        static int generation;
        sealed class Particle
        {
            internal RectTransform rt; internal Graphic graphic; internal Text text;
            internal Vector2 start,velocity,control,end;
            internal float age,life,size,rotation,spin,gravity,sway,delay,trailClock;
            internal Color color; internal int mode; internal bool unscaled,trail;
            internal RectTransform target; internal Action arrive;
            internal GameView game; internal bool gameplay;
        }
        public static void Init(RectTransform fxLayer)
        {
            ClearAll();Layer=fxLayer;if(Layer==null)return;
            if(Layer.GetComponent<VFXRunner>()==null)Layer.gameObject.AddComponent<VFXRunner>();
            // Prewarm once so typical reward bursts allocate no objects.
            for(int i=images.Count;i<128;i++){var p=Create(false);p.rt.gameObject.SetActive(false);images.Push(p);}
            for(int i=texts.Count;i<12;i++){var p=Create(true);p.rt.gameObject.SetActive(false);texts.Push(p);}
        }
        public static Sprite Sprite(string name){if(string.IsNullOrEmpty(name))return null;if(!sprites.TryGetValue(name,out var s)){s=Resources.Load<Sprite>("FX/"+name);sprites[name]=s;}return s;}
        static Particle Create(bool text)
        {
            var go=new GameObject(text?"FX Text":"FX Particle",typeof(RectTransform));var p=new Particle{rt=(RectTransform)go.transform};
            p.rt.SetParent(Layer,false);p.rt.anchorMin=p.rt.anchorMax=p.rt.pivot=new Vector2(.5f,.5f);
            if(text){p.text=go.AddComponent<Text>();p.text.alignment=TextAnchor.MiddleCenter;p.text.horizontalOverflow=HorizontalWrapMode.Overflow;p.text.verticalOverflow=VerticalWrapMode.Overflow;var shadow=go.AddComponent<Shadow>();shadow.effectColor=new Color(.29f,.18f,.15f,.45f);shadow.effectDistance=new Vector2(3,-6);var outline=go.AddComponent<Outline>();outline.effectColor=new Color(.29f,.184f,.149f);outline.effectDistance=new Vector2(4,-4);p.graphic=p.text;}
            else {var img=go.AddComponent<Image>();img.preserveAspect=true;p.graphic=img;}
            p.graphic.raycastTarget=false;return p;
        }
        static Vector2 Local(Vector3 world){return Layer.InverseTransformPoint(world);}
        static Particle Take(Vector3 world,Sprite sprite,float size,float life,Color color,bool text=false)
        {
            if(Layer==null)return null;var pool=text?texts:images;Particle p=null;
            while(pool.Count>0 && (p==null || p.rt==null))p=pool.Pop();if(p==null || p.rt==null)p=Create(text);
            p.rt.SetParent(Layer,false);p.start=Local(world);p.velocity=p.control=p.end=Vector2.zero;p.age=0;p.life=Mathf.Max(.01f,life);p.size=size;p.rotation=UnityEngine.Random.Range(-180f,180f);p.spin=p.gravity=p.sway=p.delay=p.trailClock=0;p.color=color;p.mode=0;p.unscaled=p.trail=false;p.target=null;p.arrive=null;
            var game=GameBoot.I ? GameBoot.I.CurrentGame : null;
            p.game=game && game.GameplayFxActive ? game : null;p.gameplay=p.game != null;
            if(!p.gameplay && GameBoot.I && GameBoot.I.popupLayer.childCount>0)
                p.rt.SetParent(GameBoot.I.popupFxLayer,false);
            p.rt.anchoredPosition=p.start;p.rt.sizeDelta=Vector2.one*size;p.rt.localScale=Vector3.zero;p.rt.localRotation=Quaternion.identity;p.graphic.color=color;
            if(!text)((Image)p.graphic).sprite=sprite;p.rt.gameObject.SetActive(true);active.Add(p);return p;
        }
        static void Release(int index){var p=active[index];active.RemoveAt(index);p.arrive=null;p.target=null;p.game=null;p.gameplay=false;if(p.rt==null)return;p.rt.gameObject.SetActive(false);(p.text==null?images:texts).Push(p);}
        public static void Burst(Vector3 worldPos,string sprite,int count,Color[] colors,float speedMin,float speedMax,float sizeMin,float sizeMax,float life,float gravity=0,float spin=360,float angleMin=0,float angleMax=360)
        {
            if(Layer==null)return;var s=Sprite(sprite);for(int i=0;i<count;i++){var c=colors!=null&&colors.Length>0?colors[UnityEngine.Random.Range(0,colors.Length)]:Color.white;var p=Take(worldPos,s,UnityEngine.Random.Range(sizeMin,sizeMax),life*UnityEngine.Random.Range(.85f,1.15f),c);float a=UnityEngine.Random.Range(angleMin,angleMax)*Mathf.Deg2Rad;p.velocity=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*UnityEngine.Random.Range(speedMin,speedMax);p.gravity=gravity;p.spin=UnityEngine.Random.Range(-spin,spin);}
        }
        public static void Sparkle(Vector3 worldPos,int count=10,float scale=1){Burst(worldPos,"sparkle",count,warm,100*scale,340*scale,22*scale,58*scale,.55f,0,220);}
        public static void Stars(Vector3 worldPos,int count=12,float scale=1){Burst(worldPos,"star",count,party,190*scale,520*scale,30*scale,66*scale,.95f,650*scale,420,20,160);}
        public static void Poof(Vector3 worldPos,float scale=1){if(Layer==null)return;Burst(worldPos,"smoke",8,null,40*scale,160*scale,65*scale,115*scale,.65f);for(int i=Mathf.Max(0,active.Count-8);i<active.Count;i++)active[i].mode=1;}
        public static void Hearts(Vector3 worldPos,int count=5){if(Layer==null)return;Burst(worldPos,"heart",count,null,70,150,38,65,1.3f,0,15,65,115);for(int i=Mathf.Max(0,active.Count-count);i<active.Count;i++){active[i].sway=22;active[i].rotation=UnityEngine.Random.Range(-18f,18f);}}
        public static void Steam(Vector3 worldPos,float scale=1){if(Layer==null)return;int n=UnityEngine.Random.Range(1,4);Burst(worldPos,"smoke",n,null,35*scale,85*scale,45*scale,75*scale,1.1f,0,20,75,105);for(int i=Mathf.Max(0,active.Count-n);i<active.Count;i++){active[i].mode=1;active[i].sway=12;active[i].color=new Color(1,1,1,.5f);}}
        public static void Ring(Vector3 worldPos,Color color,float size=220,float duration=.45f){var p=Take(worldPos,Sprite("ring"),size,duration,color);if(p!=null){p.mode=2;p.rotation=0;}}
        public static void Flash(Vector3 worldPos,float size=260,float duration=.3f){var p=Take(worldPos,Sprite("glow"),size,duration,Color.white);if(p!=null)p.mode=2;}
        public static void Confetti(float duration=2.5f,int perSecond=60){rainRemaining=Mathf.Max(0,duration);rainRate=Mathf.Max(0,perSecond);rainAccumulator=0;}
        public static void FloatingText(Vector3 worldPos,string text,Color color,int fontSize=64,float rise=140,float duration=1)
        {
            var p=Take(worldPos,null,fontSize,duration,color,true);if(p==null)return;p.text.font=Font!=null?Font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");p.text.text=text;p.text.fontSize=fontSize;p.rt.sizeDelta=new Vector2(1000,fontSize*2);p.mode=3;p.rotation=0;p.velocity=Vector2.up*(rise/p.life);p.unscaled=true;
        }
        public static void CoinFly(Vector3 fromWorld,RectTransform target,int count,Action onEachArrive=null,string sprite="coin")
        {
            if(Layer==null||target==null)return;for(int i=0;i<count;i++){var p=Take(fromWorld,Sprite(sprite),52,.75f,Color.white);p.mode=5;p.delay=i*.045f;p.target=target;p.end=Local(target.position);p.control=p.start+UnityEngine.Random.insideUnitCircle*150+Vector2.up*100;p.arrive=onEachArrive;p.rotation=0;}
        }
        public static void FlyIcon(Sprite icon,Vector3 fromWorld,Vector3 toWorld,float size,float duration,Action onArrive=null,float arcHeight=250,bool trail=true)
        {
            var p=Take(fromWorld,icon,size,duration,Color.white);if(p==null)return;p.mode=4;p.end=Local(toWorld);p.control=(p.start+p.end)*.5f+Vector2.up*arcHeight;p.arrive=onArrive;p.trail=trail;p.rotation=0;
        }
        public static void ScreenShake(float strength=18,float duration=.3f)
        {
            if(!ShakeRoot)return;
            var game=GameBoot.I ? GameBoot.I.CurrentGame : null;
            var shake=Tween.Shake(ShakeRoot,strength,duration);
            if(game && game.GameplayFxActive)shake.SetOwner(game);
        }
        public static void ClearAll(){generation++;for(int i=active.Count-1;i>=0;i--)Release(i);rainRemaining=rainAccumulator=0;}
        public static void ClearGameplay(GameView game)
        {
            generation++;
            for(int i=active.Count-1;i>=0;i--)if(active[i].gameplay && active[i].game==game)Release(i);
        }
        internal static void Tick(RectTransform owner)
        {
            if(Layer==null||owner!=Layer)return;
            if(rainRemaining>0){float dt=Mathf.Min(Time.unscaledDeltaTime,rainRemaining);rainRemaining-=dt;rainAccumulator+=dt*rainRate;while(rainAccumulator>=1){rainAccumulator--;var r=Layer.rect;Vector3 w=Layer.TransformPoint(new Vector3(UnityEngine.Random.Range(r.xMin,r.xMax),r.yMax+40,0));var p=Take(w,Sprite("confetti"),UnityEngine.Random.Range(20f,38f),UnityEngine.Random.Range(3f,4.5f),party[UnityEngine.Random.Range(0,party.Length)]);p.velocity=new Vector2(UnityEngine.Random.Range(-45f,45f),-UnityEngine.Random.Range(320f,500f));p.spin=UnityEngine.Random.Range(-250f,250f);p.sway=45;p.mode=6;p.unscaled=true;}}
            int version=generation;
            for(int i=active.Count-1;i>=0;i--)
            {
                var p=active[i];if(p.rt==null){Release(i);continue;}
                if(p.gameplay){if(!p.game || !p.game.isActiveAndEnabled || p.game.kitchen.ended){Release(i);continue;}if(p.game.AnimationsPaused)continue;}
                float dt=p.unscaled?Time.unscaledDeltaTime:Time.deltaTime;
                if(p.delay>0){float used=Mathf.Min(dt,p.delay);p.delay-=used;dt-=used;if(p.delay>0)continue;}
                p.age+=dt;float t=Mathf.Clamp01(p.age/p.life);
                if(p.mode==5 && p.target==null){Release(i);continue;}
                Vector2 pos=p.start+p.velocity*p.age-Vector2.up*(.5f*p.gravity*p.age*p.age);pos.x+=Mathf.Sin(p.age*7)*p.sway;
                float scale=Easing.Evaluate(Ease.OutBack,Mathf.Clamp01(t/.1f));float alpha=1-Mathf.Clamp01((t-.65f)/.35f);scale*=1-.6f*Mathf.Clamp01((t-.65f)/.35f);
                if(p.mode==1)scale=(.5f+t*1.2f)*Mathf.Clamp01(t/.1f);
                if(p.mode==2){scale=.25f+t*1.1f;alpha=1-t;}
                if(p.mode==3)scale=Easing.Evaluate(Ease.OutBack,Mathf.Clamp01(t/.22f));
                if(p.mode==4||p.mode==5)
                {
                    float f=t;Vector2 start=p.start,control=p.control;
                    if(p.mode==5){p.end=Local(p.target.position);if(t<.36f){pos=Vector2.Lerp(p.start,p.control,Easing.Evaluate(Ease.OutQuad,Mathf.Clamp01(t/.16f)));f=-1;}else{f=(t-.36f)/.64f;start=p.control;control=(start+p.end)*.5f+Vector2.up*160;}}
                    if(f>=0)pos=(1-f)*(1-f)*start+2*(1-f)*f*control+f*f*p.end;
                    scale=Mathf.Clamp01(t/.08f)*(1+.25f*Mathf.Sin(t*Mathf.PI));alpha=1;
                    if(p.trail){p.trailClock+=dt;if(p.trailClock>.06f){p.trailClock=0;Burst(Layer.TransformPoint(pos),"sparkle",1,warm,0,20,12,22,.3f);}}
                }
                p.rt.anchoredPosition=pos;p.rt.localRotation=Quaternion.Euler(0,0,p.rotation+p.spin*p.age+(p.mode==4?Mathf.Sin(t*Mathf.PI*2)*12:0));p.rt.localScale=new Vector3(scale*(p.mode==6?Mathf.Cos(p.age*9):1),scale,1);var c=p.color;c.a*=alpha;p.graphic.color=c;
                if(t>=1){var cb=p.arrive;var target=p.target;bool coin=p.mode==5;Release(i);if(coin&&target!=null)Tween.PunchScale(target,Vector3.one*.14f,.25f);try{cb?.Invoke();}catch(Exception e){Debug.LogException(e);}if(version!=generation)return;}
            }
        }
    }
    [AddComponentMenu("")]
    internal sealed class VFXRunner:MonoBehaviour { void Update(){VFX.Tick((RectTransform)transform);} }
}
