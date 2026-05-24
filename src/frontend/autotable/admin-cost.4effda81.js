const u="admin-cost-overlay";function h(t){const e=typeof t.model=="string"&&t.model!==""?t.model:typeof t.modelName=="string"&&t.modelName!==""?t.modelName:null;if(e===null)return null;const s=typeof t.costUsd=="number"&&Number.isFinite(t.costUsd)?t.costUsd:typeof t.cost=="number"&&Number.isFinite(t.cost)?t.cost:0,n=typeof t.callCount=="number"&&Number.isFinite(t.callCount)?t.callCount:typeof t.calls=="number"&&Number.isFinite(t.calls)?t.calls:NaN;return{model:e,costUsd:Math.max(0,s),callCount:Number.isFinite(n)?Math.max(0,Math.floor(n)):null}}function b(t){if(t===null||typeof t!="object")return null;const e=t,s=typeof e.currentMonthCost=="number"&&Number.isFinite(e.currentMonthCost)?Math.max(0,e.currentMonthCost):0,n=typeof e.budgetCapUsd=="number"&&Number.isFinite(e.budgetCapUsd)?Math.max(0,e.budgetCapUsd):0;let a=typeof e.percentUsed=="number"&&Number.isFinite(e.percentUsed)?e.percentUsed:n>0?s/n*100:0;a>0&&a<=1&&s>0&&n>0&&Math.abs(a-s/n)<.05&&(a=a*100),a=Math.max(0,a);const y=Array.isArray(e.byModel)?e.byModel:[],m=[];for(const d of y){if(d===null||typeof d!="object")continue;const i=h(d);i!==null&&m.push(i)}return m.sort((d,i)=>i.costUsd-d.costUsd),{currentMonthCost:s,budgetCapUsd:n,percentUsed:a,byModel:m}}function o(t){return t.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function l(t){return`$${t.toFixed(2)}`}function f(t){return`${t.toFixed(1)}%`}function C(t){return t>=95?"admin-cost-pct admin-cost-pct-critical":t>=80?"admin-cost-pct admin-cost-pct-warn":"admin-cost-pct admin-cost-pct-ok"}function g(){let t=document.getElementById(u);return t!==null?(t.innerHTML="",t):(t=document.createElement("div"),t.id=u,t.className="admin-cost-overlay",t.setAttribute("role","dialog"),t.setAttribute("aria-modal","false"),t.setAttribute("data-testid","admin-cost-overlay"),document.body.appendChild(t),t)}function p(){const t=document.getElementById(u);t!==null&&t.parentNode!==null&&t.parentNode.removeChild(t)}function r(t,e){return`
    <div class="admin-cost-card" data-testid="admin-cost-empty">
      <header class="admin-cost-header">
        <h2 class="admin-cost-title">${o(t)}</h2>
        <button type="button" class="admin-cost-close"
                data-testid="admin-cost-close"
                aria-label="Close cost panel">×</button>
      </header>
      <p class="admin-cost-empty-message">${o(e)}</p>
    </div>`}function M(t){const e=C(t.percentUsed),s=t.byModel.map(a=>`
    <tr data-testid="admin-cost-model-row" data-model="${o(a.model)}">
      <td>${o(a.model)}</td>
      <td class="admin-cost-num">${o(l(a.costUsd))}</td>
      <td class="admin-cost-num">${a.callCount!==null?o(String(a.callCount)):"—"}</td>
    </tr>`).join(""),n=s!==""?s:'<tr><td colspan="3" class="admin-cost-table-empty">No per-model cost data yet.</td></tr>';return`
    <div class="admin-cost-card" data-testid="admin-cost-card">
      <header class="admin-cost-header">
        <h2 class="admin-cost-title">Commentary cost summary</h2>
        <button type="button" class="admin-cost-close"
                data-testid="admin-cost-close"
                aria-label="Close cost panel">×</button>
      </header>
      <section class="admin-cost-summary" data-testid="admin-cost-summary">
        <div class="admin-cost-summary-line">
          <span class="admin-cost-summary-label">Current month</span>
          <strong class="admin-cost-summary-value"
                  data-testid="admin-cost-current">${o(l(t.currentMonthCost))}</strong>
          <span class="admin-cost-summary-separator">/</span>
          <span class="admin-cost-summary-cap"
                data-testid="admin-cost-cap">${o(l(t.budgetCapUsd))}</span>
          <span class="${e}"
                data-testid="admin-cost-percent">${o(f(t.percentUsed))}</span>
        </div>
      </section>
      <table class="admin-cost-table" data-testid="admin-cost-table">
        <thead>
          <tr>
            <th scope="col">Model</th>
            <th scope="col">Cost</th>
            <th scope="col">Calls</th>
          </tr>
        </thead>
        <tbody>${n}</tbody>
      </table>
    </div>`}function c(t){const e=t.querySelector(".admin-cost-close");e==null||e.addEventListener("click",()=>{p();try{const s=new URL(window.location.href);s.pathname="/",s.search="",window.history.replaceState(window.history.state,"",s.pathname+s.search+s.hash)}catch(s){}})}function v(){try{const t=new URL(window.location.href);t.pathname="/",t.search="",t.hash="",window.location.replace(t.toString())}catch(t){window.location.href="/"}}async function U(){const t=g();t.innerHTML=`
    <div class="admin-cost-card" data-testid="admin-cost-loading">
      <header class="admin-cost-header">
        <h2 class="admin-cost-title">Commentary cost summary</h2>
        <button type="button" class="admin-cost-close"
                data-testid="admin-cost-close"
                aria-label="Close cost panel">×</button>
      </header>
      <p class="admin-cost-loading-message">Loading cost summary…</p>
    </div>`,c(t);let e;try{e=await fetch("/api/commentary/cost/summary",{credentials:"same-origin",headers:{Accept:"application/json"}})}catch(a){t.innerHTML=r("Commentary cost summary","Could not reach the cost summary service."),c(t);return}if(e.status===401){p(),v();return}if(e.status===403){t.innerHTML=r("Commentary cost summary","Admins only — this surface is gated to admin accounts."),c(t);return}if(e.status===404){t.innerHTML=r("Commentary cost summary","Cost summary not available."),c(t);return}if(!e.ok){t.innerHTML=r("Commentary cost summary","Cost summary unavailable."),c(t);return}let s;try{s=await e.json()}catch(a){t.innerHTML=r("Commentary cost summary","Cost summary response malformed."),c(t);return}const n=b(s);if(n===null){t.innerHTML=r("Commentary cost summary","Cost summary response malformed."),c(t);return}t.innerHTML=M(n),c(t)}export{U as openCommentaryCostPanel};
