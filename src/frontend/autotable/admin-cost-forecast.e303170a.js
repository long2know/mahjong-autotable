const m="admin-cost-forecast-overlay";function y(t){if(t===null||typeof t!="object")return null;const e=t,a=typeof e.projectedMonthEndCostUsd=="number"&&Number.isFinite(e.projectedMonthEndCostUsd)?e.projectedMonthEndCostUsd:typeof e.projectedCostUsd=="number"&&Number.isFinite(e.projectedCostUsd)?e.projectedCostUsd:typeof e.projectedCost=="number"&&Number.isFinite(e.projectedCost)?e.projectedCost:null;if(a===null)return null;let s=typeof e.confidence=="number"&&Number.isFinite(e.confidence)?e.confidence:0;s>0&&s<=1&&(s=s*100),s=Math.max(0,Math.min(100,s));const i=typeof e.daysOfData=="number"&&Number.isFinite(e.daysOfData)?e.daysOfData:typeof e.daysWithData=="number"&&Number.isFinite(e.daysWithData)?e.daysWithData:0,r=Math.max(0,Math.floor(i)),d=typeof e.windowDays=="number"&&Number.isFinite(e.windowDays)?e.windowDays:r,f=Math.max(1,Math.floor(d)),u=typeof e.currency=="string"&&e.currency!==""?e.currency:"USD";return{projectedCostUsd:Math.max(0,a),confidence:s,daysOfData:r,windowDays:f,currency:u}}function c(t){return t.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function p(t,e){return e==="USD"?`$${t.toFixed(2)}`:`${e} ${t.toFixed(2)}`}function h(t){return`${t.toFixed(1)}%`}function b(t){return t>=80?"admin-cost-forecast-conf admin-cost-forecast-conf-strong":t>=50?"admin-cost-forecast-conf admin-cost-forecast-conf-moderate":"admin-cost-forecast-conf admin-cost-forecast-conf-weak"}function w(){let t=document.getElementById(m);return t!==null?(t.innerHTML="",t):(t=document.createElement("div"),t.id=m,t.className="admin-cost-forecast-overlay",t.setAttribute("role","dialog"),t.setAttribute("aria-modal","false"),t.setAttribute("data-testid","admin-cost-forecast-overlay"),document.body.appendChild(t),t)}function l(){const t=document.getElementById(m);t!==null&&t.parentNode!==null&&t.parentNode.removeChild(t)}function n(t,e){return`
    <div class="admin-cost-forecast-card" data-testid="admin-cost-forecast-empty">
      <header class="admin-cost-forecast-header">
        <h2 class="admin-cost-forecast-title">${c(t)}</h2>
        <button type="button" class="admin-cost-forecast-close"
                data-testid="admin-cost-forecast-close"
                aria-label="Close forecast panel">×</button>
      </header>
      <p class="admin-cost-forecast-empty-message">${c(e)}</p>
    </div>`}function C(t){const e=b(t.confidence);return`
    <div class="admin-cost-forecast-card" data-testid="admin-cost-forecast-card">
      <header class="admin-cost-forecast-header">
        <h2 class="admin-cost-forecast-title">Commentary cost forecast</h2>
        <button type="button" class="admin-cost-forecast-close"
                data-testid="admin-cost-forecast-close"
                aria-label="Close forecast panel">×</button>
      </header>
      <section class="admin-cost-forecast-summary"
               data-testid="admin-cost-forecast-summary">
        <div class="admin-cost-forecast-summary-line">
          <span class="admin-cost-forecast-summary-label">Projected month-end</span>
          <strong class="admin-cost-forecast-summary-value"
                  data-testid="admin-cost-forecast-projected">${c(p(t.projectedCostUsd,t.currency))}</strong>
        </div>
        <div class="admin-cost-forecast-summary-line">
          <span class="admin-cost-forecast-summary-label">Confidence</span>
          <span class="${e}"
                data-testid="admin-cost-forecast-confidence">${c(h(t.confidence))}</span>
        </div>
        <div class="admin-cost-forecast-summary-line">
          <span class="admin-cost-forecast-summary-label">Days of data</span>
          <span class="admin-cost-forecast-summary-value"
                data-testid="admin-cost-forecast-days">${c(String(t.daysOfData))} / ${c(String(t.windowDays))}</span>
        </div>
      </section>
    </div>`}function o(t){const e=t.querySelector(".admin-cost-forecast-close");e==null||e.addEventListener("click",()=>{l();try{const a=new URL(window.location.href);a.pathname="/",a.search="",window.history.replaceState(window.history.state,"",a.pathname+a.search+a.hash)}catch(a){}})}function v(){try{const t=new URL(window.location.href);t.pathname="/",t.search="",t.hash="",window.location.replace(t.toString())}catch(t){window.location.href="/"}}function g(t){const e=typeof t=="number"?t:Number(t);if(!Number.isFinite(e))return 30;const a=Math.floor(e);return a<1?1:a>90?90:a}async function M(t){const e=g(t),a=w();a.innerHTML=`
    <div class="admin-cost-forecast-card" data-testid="admin-cost-forecast-loading">
      <header class="admin-cost-forecast-header">
        <h2 class="admin-cost-forecast-title">Commentary cost forecast</h2>
        <button type="button" class="admin-cost-forecast-close"
                data-testid="admin-cost-forecast-close"
                aria-label="Close forecast panel">×</button>
      </header>
      <p class="admin-cost-forecast-loading-message">Forecasting against ${e}-day window…</p>
    </div>`,o(a);let s;try{s=await fetch(`/api/commentary/cost/forecast?days=${e}`,{credentials:"same-origin",headers:{Accept:"application/json"}})}catch(d){a.innerHTML=n("Commentary cost forecast","Could not reach the cost forecast service."),o(a);return}if(s.status===401){l(),v();return}if(s.status===400){a.innerHTML=n("Commentary cost forecast","Invalid forecast window — pick a value between 1 and 90 days."),o(a);return}if(s.status===403){a.innerHTML=n("Commentary cost forecast","Admins only — this surface is gated to admin accounts."),o(a);return}if(s.status===404){a.innerHTML=n("Commentary cost forecast","Cost forecast not available."),o(a);return}if(!s.ok){a.innerHTML=n("Commentary cost forecast","Cost forecast unavailable."),o(a);return}let i;try{i=await s.json()}catch(d){a.innerHTML=n("Commentary cost forecast","Cost forecast response malformed."),o(a);return}const r=y(i);if(r===null){a.innerHTML=n("Commentary cost forecast","Cost forecast response malformed."),o(a);return}a.innerHTML=C(r),o(a)}export{g as normaliseDays,M as openCommentaryCostForecastPanel};
