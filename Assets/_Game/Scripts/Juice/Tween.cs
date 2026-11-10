using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    public enum Ease { Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, InBack, OutBack, InOutBack, OutElastic, OutBounce, InSine, OutSine, InOutSine, OutExpo, InExpo }
    public static class Easing
    {
        public static float Evaluate(Ease e, float t)
        {
            t = Mathf.Clamp01(t); float c = 1.70158f; float u = t - 1f;
            switch (e)
            {
                case Ease.InQuad: return t*t;
                case Ease.OutQuad: return t*(2-t);
                case Ease.InOutQuad: return t < .5f ? 2*t*t : 1-Mathf.Pow(-2*t+2,2)/2;
                case Ease.InCubic: return t*t*t;
                case Ease.OutCubic: return 1+u*u*u;
                case Ease.InOutCubic: return t < .5f ? 4*t*t*t : 1+4*u*u*u;
                case Ease.InBack: return (c+1)*t*t*t-c*t*t;
                case Ease.OutBack: return 1+(c+1)*u*u*u+c*u*u;
                case Ease.InOutBack: c *= 1.525f; return t < .5f ? Mathf.Pow(2*t,2)*((c+1)*2*t-c)/2 : (Mathf.Pow(2*t-2,2)*((c+1)*(2*t-2)+c)+2)/2;
                case Ease.OutElastic: return t == 0 || t == 1 ? t : Mathf.Pow(2,-10*t)*Mathf.Sin((10*t-.75f)*2*Mathf.PI/3)+1;
                case Ease.OutBounce: if(t<1/2.75f)return 7.5625f*t*t; if(t<2/2.75f){t-=1.5f/2.75f;return 7.5625f*t*t+.75f;} if(t<2.5f/2.75f){t-=2.25f/2.75f;return 7.5625f*t*t+.9375f;} t-=2.625f/2.75f;return 7.5625f*t*t+.984375f;
                case Ease.InSine: return 1-Mathf.Cos(t*Mathf.PI/2);
                case Ease.OutSine: return Mathf.Sin(t*Mathf.PI/2);
                case Ease.InOutSine: return (1-Mathf.Cos(Mathf.PI*t))/2;
                case Ease.OutExpo: return t == 1 ? 1 : 1-Mathf.Pow(2,-10*t);
                case Ease.InExpo: return t == 0 ? 0 : Mathf.Pow(2,10*t-10);
                default: return t;
            }
        }
    }
    public class TweenHandle
    {
        public bool IsDone { get; private set; }
        public object Target { get; private set; }
        internal string Kind;
        readonly Action<float> setter;
        readonly float duration;
        float elapsed, delay;
        int loops=1, cycle;
        bool yoyo, unscaled;
        Ease ease;
        Action complete;
        Action cancel;
        Action<float> update;
        UnityEngine.Object owner;
        bool owned;
        GameView game;
        internal TweenHandle(float seconds, Action<float> apply, object target) { duration=Mathf.Max(0,seconds);setter=apply;Target=target;SetOwner(target as UnityEngine.Object); }
        public TweenHandle SetOwner(UnityEngine.Object value)
        {
            owner=value;owned=!ReferenceEquals(value,null);
            var c=value as Component;var go=value as GameObject;
            var tr=c ? c.transform : go ? go.transform : null;
            game=GameView.AnimationOwner(tr);
            return this;
        }
        public TweenHandle SetEase(Ease e){ease=e;return this;}
        public TweenHandle SetDelay(float seconds){delay=Mathf.Max(0,seconds);return this;}
        public TweenHandle SetLoops(int loops,bool yoyo=false){this.loops=loops == -1 ? -1 : Mathf.Max(1,loops);this.yoyo=yoyo;return this;}
        public TweenHandle SetUnscaled(bool unscaled=true){this.unscaled=unscaled;return this;}
        public TweenHandle OnComplete(Action a){complete+=a;return this;}
        public TweenHandle OnCancel(Action a){cancel+=a;return this;}
        public TweenHandle OnUpdate(Action<float> a){update+=a;return this;}
        internal bool Missing { get { return owned && owner == null || Target is UnityEngine.Object o && o == null; } }
        internal bool OwnedBy(Transform root)
        {
            var c=owner as Component;var go=owner as GameObject;
            var tr=c ? c.transform : go ? go.transform : null;
            return tr && (tr==root || tr.IsChildOf(root));
        }
        void Apply(float t){if(IsDone)return;if(Missing){Kill();return;}float p=Easing.Evaluate(ease,t);setter?.Invoke(p);if(Missing){Kill();return;}if(!IsDone)update?.Invoke(p);}
        public void Kill(bool complete=false)
        {
            if(IsDone)return;
            if(complete && !Missing){IsDone=true;float p=Easing.Evaluate(ease,yoyo && loops>0 && loops%2==0 ? 0 : 1);setter?.Invoke(p);if(Missing)return;update?.Invoke(p);if(!Missing)this.complete?.Invoke();}
            else {IsDone=true;cancel?.Invoke();}
        }
        internal void Tick(float scaled,float real)
        {
            if(IsDone)return;if(Missing){Kill();return;}
            if(!unscaled && game != null) { if(game.kitchen.ended){Kill();return;} if(game.AnimationsPaused)return; }
            float dt=unscaled?real:scaled;
            if(delay>0){float used=Mathf.Min(delay,dt);delay-=used;dt-=used;if(delay>0)return;}
            elapsed+=dt;
            if(duration<=0){Apply(yoyo && cycle%2==1?0:1);if(!IsDone){IsDone=true;complete?.Invoke();}return;}
            while(elapsed>=duration && !IsDone)
            {
                Apply(yoyo && cycle%2==1?0:1);if(IsDone)return;
                elapsed-=duration;cycle++;
                if(loops!=-1 && cycle>=loops){IsDone=true;complete?.Invoke();return;}
            }
            if(!IsDone)Apply(yoyo && cycle%2==1 ? 1-elapsed/duration : elapsed/duration);
        }
    }
    public static class Tween
    {
        static readonly List<TweenHandle> active=new List<TweenHandle>();
        static TweenRunner runner;
        static TweenHandle Add(float d,Action<float> setter,object target)
        {
            if(runner==null){var go=new GameObject("BepJam Tweens"){hideFlags=HideFlags.HideInHierarchy};UnityEngine.Object.DontDestroyOnLoad(go);runner=go.AddComponent<TweenRunner>();}
            var h=new TweenHandle(d,setter,target);active.Add(h);return h;
        }
        internal static void UpdateAll(){int n=active.Count;for(int i=0;i<n;i++){try{active[i].Tick(Time.deltaTime,Time.unscaledDeltaTime);}catch(Exception e){active[i].Kill();Debug.LogException(e);}}active.RemoveAll(h=>h.IsDone);}
        public static TweenHandle Float(float from,float to,float duration,Action<float> setter,object target=null){return Add(duration,p=>setter(Mathf.LerpUnclamped(from,to,p)),target);}
        public static TweenHandle Scale(Transform t,Vector3 to,float duration){Vector3 a=t.localScale;return Add(duration,p=>t.localScale=Vector3.LerpUnclamped(a,to,p),t);}
        public static TweenHandle Scale(Transform t,float to,float duration){return Scale(t,Vector3.one*to,duration);}
        public static TweenHandle Move(RectTransform rt,Vector2 toAnchored,float duration){Vector2 a=rt.anchoredPosition;return Add(duration,p=>rt.anchoredPosition=Vector2.LerpUnclamped(a,toAnchored,p),rt);}
        public static TweenHandle MoveWorld(Transform t,Vector3 toWorld,float duration){Vector3 a=t.position;return Add(duration,p=>t.position=Vector3.LerpUnclamped(a,toWorld,p),t);}
        public static TweenHandle MoveLocal(Transform t,Vector3 toLocal,float duration){Vector3 a=t.localPosition;return Add(duration,p=>t.localPosition=Vector3.LerpUnclamped(a,toLocal,p),t);}
        public static TweenHandle Rotate(Transform t,float toZDegrees,float duration){Vector3 a=t.localEulerAngles;return Add(duration,p=>t.localEulerAngles=new Vector3(a.x,a.y,Mathf.LerpUnclamped(a.z,toZDegrees,p)),t);}
        public static TweenHandle Fade(CanvasGroup cg,float to,float duration){return Float(cg.alpha,to,duration,v=>cg.alpha=v,cg);}
        public static TweenHandle Fade(Graphic g,float toAlpha,float duration){return Float(g.color.a,toAlpha,duration,v=>{var c=g.color;c.a=v;g.color=c;},g);}
        public static TweenHandle Color(Graphic g,UnityEngine.Color to,float duration){var a=g.color;return Add(duration,p=>g.color=UnityEngine.Color.LerpUnclamped(a,to,p),g);}
        public static TweenHandle Fill(Image img,float to,float duration){return Float(img.fillAmount,to,duration,v=>img.fillAmount=v,img);}
        public static TweenHandle Bezier(Transform t,Vector3 from,Vector3 control,Vector3 to,float duration){return Add(duration,p=>t.position=(1-p)*(1-p)*from+2*(1-p)*p*control+p*p*to,t);}
        static void Restore(Transform t,string kind){for(int i=active.Count-1;i>=0;i--)if(ReferenceEquals(active[i].Target,t)&&active[i].Kind==kind)active[i].Kill(true);}
        static TweenHandle Special(Transform t,string kind,float d,Action<float> setter){var h=Add(d,setter,t);h.Kind=kind;return h;}
        public static TweenHandle PunchScale(Transform t,Vector3 punch,float duration,int vibrato=6,float elasticity=.6f){Restore(t,"punchScale");var a=t.localScale;return Special(t,"punchScale",duration,p=>t.localScale=a+punch*(p>=1?0:Mathf.Sin(p*Mathf.PI*Mathf.Max(1,vibrato))*Mathf.Pow(1-p,Mathf.Lerp(4,1,Mathf.Clamp01(elasticity)))));}
        public static TweenHandle PunchRotation(Transform t,float degrees,float duration,int vibrato=6){Restore(t,"punchRotation");var a=t.localEulerAngles;return Special(t,"punchRotation",duration,p=>t.localEulerAngles=a+Vector3.forward*(p>=1?0:degrees*Mathf.Sin(p*Mathf.PI*Mathf.Max(1,vibrato))*(1-p)*(1-p)));}
        public static TweenHandle Shake(Transform t,float strength,float duration,int vibrato=20){Restore(t,"shake");var a=t.localPosition;float seed=UnityEngine.Random.value*100;return Special(t,"shake",duration,p=>t.localPosition=a+(p>=1?Vector3.zero:new Vector3(Mathf.PerlinNoise(seed,p*vibrato)*2-1,Mathf.PerlinNoise(seed+30,p*vibrato)*2-1,0)*strength*(1-p)));}
        public static TweenHandle Squash(Transform t,float amount=.2f,float duration=.35f){Restore(t,"squash");var a=t.localScale;return Special(t,"squash",duration,p=>{float w=p>=1?0:Mathf.Sin(p*Mathf.PI*3)*(1-p)*amount;t.localScale=Vector3.Scale(a,new Vector3(1+w,1-w,1));});}
        public static TweenHandle Delay(float seconds,Action action,bool unscaled=false,UnityEngine.Object owner=null){return Add(0,null,null).SetOwner(owner).SetDelay(seconds).SetUnscaled(unscaled).OnComplete(action);}
        public static void KillHierarchy(Transform root)
        {
            if(!root)return;
            foreach(var h in active.ToArray())
            {
                var c=h.Target as Component;var go=h.Target as GameObject;
                var tr=c ? c.transform : go ? go.transform : null;
                if(tr && (tr==root || tr.IsChildOf(root)))h.Kill();
                else if(h.OwnedBy(root))h.Kill();
            }
        }
        public static void KillAll(object target,bool complete=false){var copy=active.ToArray();foreach(var h in copy)if(ReferenceEquals(h.Target,target))h.Kill(complete);}
        public static void KillAllTweens(){var copy=active.ToArray();foreach(var h in copy)h.Kill();}
    }
    [AddComponentMenu("")]
    internal sealed class TweenRunner:MonoBehaviour { void Update(){Tween.UpdateAll();} }
}
