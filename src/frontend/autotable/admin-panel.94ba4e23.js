var U=Object.defineProperty;var w=Object.getOwnPropertySymbols;var K=Object.prototype.hasOwnProperty,L=Object.prototype.propertyIsEnumerable;var T=(e,t,n)=>t in e?U(e,t,{enumerable:!0,configurable:!0,writable:!0,value:n}):e[t]=n,S=(e,t)=>{for(var n in t||(t={}))K.call(t,n)&&T(e,n,t[n]);if(w)for(var n of w(t))L.call(t,n)&&T(e,n,t[n]);return e};const E="X-Admin-Reason",v="admin-panel-overlay";function l(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function f(e){if(typeof e!="string"||e==="")return"—";const t=new Date(e);if(Number.isNaN(t.getTime()))return l(e);const n=i=>i.toString().padStart(2,"0");return`${t.getUTCFullYear()}-${n(t.getUTCMonth()+1)}-${n(t.getUTCDate())} ${n(t.getUTCHours())}:${n(t.getUTCMinutes())}Z`}function R(){let e=document.getElementById(v);return e!==null?(e.innerHTML="",e):(e=document.createElement("div"),e.id=v,e.className="admin-panel-overlay",e.setAttribute("role","dialog"),e.setAttribute("aria-modal","false"),e.setAttribute("data-testid","admin-panel-overlay"),e.style.cssText="position:fixed;inset:0;background:rgba(8,12,18,0.86);display:flex;flex-direction:column;z-index:9990;overflow:auto;color:#eaeaea;font-family:system-ui,Segoe UI,Helvetica,Arial,sans-serif;",document.body.appendChild(e),e)}function N(){const e=document.getElementById(v);e!==null&&e.parentNode!==null&&e.parentNode.removeChild(e)}async function $(e,t={}){var a,r,o;let n;try{n=await fetch(e,{method:(a=t.method)!=null?a:"GET",credentials:"same-origin",headers:S({Accept:"application/json"},(r=t.headers)!=null?r:{}),body:t.body})}catch(d){return{ok:!1,placeholderHtml:h("Network error","Admin endpoint unreachable.  Retry from the toolbar.")}}if(n.status===401)return H(),{ok:!1,status:401,placeholderHtml:""};if(n.status===403)return{ok:!1,status:403,placeholderHtml:h("Admins only","This panel is reserved for users with the admin role.")};if(n.status===503)return{ok:!1,status:503,placeholderHtml:h("Surface disabled","The per-tenant store for this surface is not registered on this deployment.  See Bishop's W17 controller docs.")};if(n.status===204)return{ok:!0,status:204};if(!n.ok){let d="";try{const s=await n.json();d=`${(o=s.error)!=null?o:""}${s.detail!==void 0?`: ${s.detail}`:""}`}catch(s){d=`HTTP ${n.status}`}return{ok:!1,status:n.status,placeholderHtml:h(`Request failed (${n.status})`,d||"No detail returned.")}}let i;try{i=await n.json()}catch(d){i=null}return{ok:!0,status:n.status,body:i}}function H(){try{window.location.assign("/")}catch(e){}}function h(e,t){return`
    <div class="admin-panel-placeholder" data-testid="admin-panel-placeholder">
      <h3>${l(e)}</h3>
      <p>${l(t)}</p>
    </div>`}function M(e){const t=window.prompt(`Enter a short X-Admin-Reason for this ${e}.
Required by Bishop's W17 audit contract.`,"");if(t===null)return null;const n=t.trim();return n===""?null:n}function q(e,t){if(t.length===0)return`
      <div class="admin-panel-list" data-testid="admin-panel-${e.id}-list">
        <p class="admin-panel-empty">No policies recorded.  Use
        <strong>Create</strong> to add one.</p>
      </div>`;const n=e.columns.map(a=>`<th scope="col">${l(a.label)}</th>`).join(""),i=t.map(a=>{const r=e.rowKey(a),o=e.columns.map(d=>{const s=d.render(a);return`<td>${typeof s=="string"?l(s):s.__html}</td>`}).join("");return`
      <tr data-testid="admin-panel-${e.id}-row"
          data-tenant-id="${l(r)}">
        ${o}
        <td>
          <button type="button"
                  class="admin-panel-btn"
                  data-testid="admin-panel-${e.id}-edit"
                  data-tenant-id="${l(r)}"
                  data-action="edit">Edit</button>
          <button type="button"
                  class="admin-panel-btn admin-panel-btn-danger"
                  data-testid="admin-panel-${e.id}-delete"
                  data-tenant-id="${l(r)}"
                  data-action="delete">Delete</button>
        </td>
      </tr>`}).join("");return`
    <div class="admin-panel-list" data-testid="admin-panel-${e.id}-list">
      <table class="admin-panel-table">
        <thead><tr>${n}<th scope="col">Actions</th></tr></thead>
        <tbody>${i}</tbody>
      </table>
    </div>`}function x(e,t,n){const i=e.fields.map(a=>{var u,m;const r=(u=n[a.name])!=null?u:"",o=t==="edit"&&a.primaryKey===!0,d=`admin-panel-${e.id}-${a.name}`,s=` id="${d}" name="${l(a.name)}" data-testid="${d}"`+(o?" readonly":"")+(a.required===!0?" required":"")+(a.placeholder!==void 0?` placeholder="${l(a.placeholder)}"`:"");let c;if(a.type==="select"){const y=((m=a.options)!=null?m:[]).map(b=>`<option value="${l(b.value)}"`+(b.value===r?" selected":"")+`>${l(b.label)}</option>`).join("");c=`<select${s}>${y}</select>`}else a.type==="number"?c=`<input type="number"${s}`+(a.min!==void 0?` min="${a.min}"`:"")+(a.max!==void 0?` max="${a.max}"`:"")+(a.integer===!0?' step="1"':"")+` value="${l(r)}" />`:a.type==="datetime-local"?c=`<input type="datetime-local"${s} value="${l(r)}" />`:c=`<input type="text"${s} value="${l(r)}" />`;return`
      <div class="admin-panel-field">
        <label for="${d}">${l(a.label)}${a.required===!0?" *":""}</label>
        ${c}
        ${a.help!==void 0?`<small class="admin-panel-help">${l(a.help)}</small>`:""}
      </div>`}).join("");return`
    <form class="admin-panel-form"
          data-testid="admin-panel-${e.id}-form"
          data-mode="${t}">
      <h3>${l(t==="create"?"Create policy":"Edit policy")}</h3>
      ${i}
      <div class="admin-panel-form-actions">
        <button type="submit"
                class="admin-panel-btn admin-panel-btn-primary"
                data-testid="admin-panel-${e.id}-save">
          ${l(t==="create"?"Create":"Save")}
        </button>
        <button type="button"
                class="admin-panel-btn"
                data-testid="admin-panel-${e.id}-cancel"
                data-action="cancel">Cancel</button>
      </div>
    </form>`}function O(e){const t={};for(const n of Array.from(e.elements))(n instanceof HTMLInputElement||n instanceof HTMLSelectElement||n instanceof HTMLTextAreaElement)&&n.name!==""&&(t[n.name]=n.value);return t}function _(e){if(e===null||typeof e!="object")return null;const t=e,n=typeof t.tenantId=="string"?t.tenantId:null,i=typeof t.retentionDays=="number"&&Number.isFinite(t.retentionDays)?Math.floor(t.retentionDays):null;return n===null||i===null?null:{tenantId:n,retentionDays:i,createdAt:typeof t.createdAt=="string"?t.createdAt:typeof t.createdAtOffset=="string"?t.createdAtOffset:void 0,updatedAt:typeof t.updatedAt=="string"?t.updatedAt:typeof t.updatedAtOffset=="string"?t.updatedAtOffset:void 0}}const W={id:"replay-retention",title:"Replay retention policies",description:"Per-tenant TTL for completed replays.  Bishop W17 — rows older than the configured day-count are swept by the replay-retention background job.",endpoint:"/api/admin/replays/retention",parseRow:_,rowKey:e=>e.tenantId,rowToFormValues:e=>({tenantId:e.tenantId,retentionDays:String(e.retentionDays)}),buildBody:e=>{var t;return{tenantId:((t=e.tenantId)!=null?t:"").trim(),retentionDays:Math.max(1,Math.floor(Number(e.retentionDays)))}},fields:[{name:"tenantId",label:"Tenant ID",type:"text",required:!0,primaryKey:!0,placeholder:"tenant-acme",help:"Matches Replays.TenantId — case-sensitive."},{name:"retentionDays",label:"Retention (days)",type:"number",required:!0,min:1,max:365*5,integer:!0,placeholder:"90",help:"Upper bound 1825 (5 years) enforced server-side."}],columns:[{key:"tenantId",label:"Tenant",render:e=>e.tenantId},{key:"retentionDays",label:"Days",render:e=>({__html:`<span class="admin-panel-num">${l(String(e.retentionDays))}</span>`})},{key:"updatedAt",label:"Updated",render:e=>f(e.updatedAt)},{key:"createdAt",label:"Created",render:e=>f(e.createdAt)}]};function j(e){if(e===null||typeof e!="object")return null;const t=e,n=typeof t.tenantId=="string"?t.tenantId:null,i=typeof t.activeKid=="string"?t.activeKid:null;return n===null||i===null?null:{tenantId:n,activeKid:i,previousKid:typeof t.previousKid=="string"?t.previousKid:"",rotationStartUtc:typeof t.rotationStartUtc=="string"?t.rotationStartUtc:"",rotationCompleteUtc:typeof t.rotationCompleteUtc=="string"?t.rotationCompleteUtc:"",overlapWindowDays:typeof t.overlapWindowDays=="number"&&Number.isFinite(t.overlapWindowDays)?Math.max(0,Math.floor(t.overlapWindowDays)):0,createdAt:typeof t.createdAt=="string"?t.createdAt:void 0,updatedAt:typeof t.updatedAt=="string"?t.updatedAt:void 0}}function D(e){if(e===void 0||e==="")return"";const t=new Date(e);if(Number.isNaN(t.getTime()))return"";const n=i=>i.toString().padStart(2,"0");return`${t.getUTCFullYear()}-${n(t.getUTCMonth()+1)}-${n(t.getUTCDate())}T${n(t.getUTCHours())}:${n(t.getUTCMinutes())}`}function k(e){if(e==="")return new Date().toISOString();const t=e.endsWith("Z")?e:`${e}:00Z`,n=new Date(t);return Number.isNaN(n.getTime())?new Date().toISOString():n.toISOString()}const B={id:"jwks-rotation",title:"Per-tenant JWKS rotation policies",description:"Stages a per-tenant active/previous KID overlap window.  Bishop W16/W17 — the validator gates JWT issue against this row and blocks (`stale_per_tenant_policy`) when the rotation has gone stale.",endpoint:"/api/admin/jwks-rotation/per-tenant",parseRow:j,rowKey:e=>e.tenantId,rowToFormValues:e=>({tenantId:e.tenantId,activeKid:e.activeKid,previousKid:e.previousKid,rotationStartUtc:D(e.rotationStartUtc),rotationCompleteUtc:D(e.rotationCompleteUtc),overlapWindowDays:String(e.overlapWindowDays)}),buildBody:e=>{var t,n,i,a,r,o;return{tenantId:((t=e.tenantId)!=null?t:"").trim(),activeKid:((n=e.activeKid)!=null?n:"").trim(),previousKid:((i=e.previousKid)!=null?i:"").trim(),rotationStartUtc:k((a=e.rotationStartUtc)!=null?a:""),rotationCompleteUtc:k((r=e.rotationCompleteUtc)!=null?r:""),overlapWindowDays:Math.max(0,Math.floor(Number((o=e.overlapWindowDays)!=null?o:"0")))}},fields:[{name:"tenantId",label:"Tenant ID",type:"text",required:!0,primaryKey:!0,placeholder:"tenant-acme"},{name:"activeKid",label:"Active KID",type:"text",required:!0,placeholder:"kid-2026-05",help:"Current signing key ID."},{name:"previousKid",label:"Previous KID",type:"text",placeholder:"kid-2026-04",help:"Optional — the KID that was active before the rotation began."},{name:"rotationStartUtc",label:"Rotation start (UTC)",type:"datetime-local",required:!0,help:"Inputs are treated as UTC — local time-zone NOT applied."},{name:"rotationCompleteUtc",label:"Rotation complete (UTC)",type:"datetime-local",required:!0,help:"Must strictly follow rotation start."},{name:"overlapWindowDays",label:"Overlap window (days)",type:"number",required:!0,min:0,max:90,integer:!0,placeholder:"7",help:"Grace period during which previous KID is still accepted."}],columns:[{key:"tenantId",label:"Tenant",render:e=>e.tenantId},{key:"activeKid",label:"Active KID",render:e=>({__html:`<code>${l(e.activeKid)}</code>`})},{key:"previousKid",label:"Previous KID",render:e=>e.previousKid===""?{__html:'<span class="admin-panel-muted">—</span>'}:{__html:`<code>${l(e.previousKid)}</code>`}},{key:"rotationCompleteUtc",label:"Completes",render:e=>f(e.rotationCompleteUtc)},{key:"overlapWindowDays",label:"Overlap",render:e=>`${e.overlapWindowDays}d`}]};function F(e){if(e===null||typeof e!="object")return null;const t=e,n=typeof t.tenantId=="string"?t.tenantId:null,i=typeof t.retentionMinutes=="number"&&Number.isFinite(t.retentionMinutes)?Math.floor(t.retentionMinutes):null;return n===null||i===null?null:{tenantId:n,retentionMinutes:i,createdAt:typeof t.createdAt=="string"?t.createdAt:typeof t.createdAtOffset=="string"?t.createdAtOffset:void 0,updatedAt:typeof t.updatedAt=="string"?t.updatedAt:typeof t.updatedAtOffset=="string"?t.updatedAtOffset:void 0}}function P(e){if(e<60)return`${e}m`;const t=Math.floor(e/60),n=e%60;return n===0?`${t}h`:`${t}h${n.toString().padStart(2,"0")}m`}const V={id:"signalr-retention",title:"SignalR sequence retention policies",description:"Per-tenant SignalR sequence-entry TTL (reconnect window).  Bishop W17 — overrides the global SequenceRetention knob for enterprise tenants that need longer-lived reconnect tokens.",endpoint:"/api/admin/signalr/retention",parseRow:F,rowKey:e=>e.tenantId,rowToFormValues:e=>({tenantId:e.tenantId,retentionMinutes:String(e.retentionMinutes)}),buildBody:e=>{var t;return{tenantId:((t=e.tenantId)!=null?t:"").trim(),retentionMinutes:Math.max(1,Math.floor(Number(e.retentionMinutes)))}},fields:[{name:"tenantId",label:"Tenant ID",type:"text",required:!0,primaryKey:!0,placeholder:"tenant-acme",help:"Empty string falls through to global default (back-compat)."},{name:"retentionMinutes",label:"Retention (minutes)",type:"number",required:!0,min:1,max:60*24*30,integer:!0,placeholder:"1440",help:"Common values: 60 (free-tier), 1440 (24h), 10080 (1 week)."}],columns:[{key:"tenantId",label:"Tenant",render:e=>e.tenantId},{key:"retentionMinutes",label:"Retention",render:e=>({__html:`<span class="admin-panel-num">${l(P(e.retentionMinutes))}</span> <small class="admin-panel-muted">(${e.retentionMinutes}m)</small>`})},{key:"updatedAt",label:"Updated",render:e=>f(e.updatedAt)},{key:"createdAt",label:"Created",render:e=>f(e.createdAt)}]},I=[W,B,V];let A=0;async function te(){const e=R();e.innerHTML=J(),Y(e),await p(e)}function J(){return`
    <div class="admin-panel-shell" data-testid="admin-panel-shell">
      <header class="admin-panel-header">
        <h1 class="admin-panel-title">Admin · Tenant policies</h1>
        <button type="button"
                class="admin-panel-close"
                data-testid="admin-panel-close"
                aria-label="Close admin panel">×</button>
      </header>
      <nav class="admin-panel-tabs" role="tablist">
        ${I.map((t,n)=>`
    <button type="button"
            class="admin-panel-tab${n===A?" admin-panel-tab-active":""}"
            data-testid="admin-panel-tab-${t.id}"
            data-surface-index="${n}">
      ${l(t.title)}
    </button>`).join("")}
      </nav>
      <section class="admin-panel-body" data-testid="admin-panel-body">
        <p>Loading…</p>
      </section>
    </div>`}function Y(e){e.addEventListener("click",t=>{const n=t.target;if(n instanceof HTMLElement){if(n.classList.contains("admin-panel-close")){N();return}if(n.classList.contains("admin-panel-tab")){const i=Number(n.getAttribute("data-surface-index"));if(Number.isInteger(i)&&i>=0&&i<I.length){A=i;const a=e.querySelector(".admin-panel-tabs");a!==null&&a.querySelectorAll(".admin-panel-tab").forEach((o,d)=>{o.classList.toggle("admin-panel-tab-active",d===i)}),p(e)}}}})}async function p(e){var r;const t=I[A],n=e.querySelector(".admin-panel-body");if(n===null)return;n.innerHTML=G(t);const i=await $(t.endpoint);if(!i.ok){if(i.status===401)return;n.innerHTML=g(t,(r=i.placeholderHtml)!=null?r:"");return}const a=Z(t,i.body);n.innerHTML=g(t,q(t,a)),z(n,t,a)}function G(e){return g(e,`
    <p class="admin-panel-loading"
       data-testid="admin-panel-${e.id}-loading">
      Loading ${l(e.title.toLowerCase())}…
    </p>`)}function g(e,t){return`
    <article class="admin-panel-surface"
             data-testid="admin-panel-surface-${e.id}">
      <header>
        <h2>${l(e.title)}</h2>
        <p class="admin-panel-description">${l(e.description)}</p>
      </header>
      <div class="admin-panel-toolbar">
        <button type="button"
                class="admin-panel-btn admin-panel-btn-primary"
                data-testid="admin-panel-${e.id}-create"
                data-action="create">+ Create</button>
        <button type="button"
                class="admin-panel-btn"
                data-testid="admin-panel-${e.id}-refresh"
                data-action="refresh">Refresh</button>
      </div>
      <div class="admin-panel-surface-body"
           data-testid="admin-panel-${e.id}-content">
        ${t}
      </div>
    </article>`}function Z(e,t){if(t===null||typeof t!="object")return[];const n=t,i=Array.isArray(n.items)?n.items:[],a=[];for(const r of i){const o=e.parseRow(r);o!==null&&a.push(o)}return a}function z(e,t,n){const i=e.querySelector(`[data-testid="admin-panel-${t.id}-create"]`);i==null||i.addEventListener("click",()=>{C(e,t,"create",{})});const a=e.querySelector(`[data-testid="admin-panel-${t.id}-refresh"]`);a==null||a.addEventListener("click",()=>{const r=document.getElementById("admin-panel-overlay");r!==null&&p(r)}),e.querySelectorAll(`[data-testid="admin-panel-${t.id}-edit"]`).forEach(r=>{r.addEventListener("click",()=>{var c;const o=(c=r.getAttribute("data-tenant-id"))!=null?c:"",d=n.find(u=>t.rowKey(u)===o);if(d===void 0)return;const s=t.rowToFormValues!==void 0?t.rowToFormValues(d):{tenantId:o};C(e,t,"edit",s)})}),e.querySelectorAll(`[data-testid="admin-panel-${t.id}-delete"]`).forEach(r=>{r.addEventListener("click",()=>{var d;const o=(d=r.getAttribute("data-tenant-id"))!=null?d:"";o!==""&&Q(t,o)})})}function C(e,t,n,i){const a=e.querySelector(`[data-testid="admin-panel-${t.id}-content"]`);if(a===null)return;a.innerHTML=x(t,n,i);const r=a.querySelector("form");if(r===null)return;r.addEventListener("submit",d=>{d.preventDefault();const s=O(r);X(t,n,s)});const o=a.querySelector(`[data-testid="admin-panel-${t.id}-cancel"]`);o==null||o.addEventListener("click",()=>{const d=document.getElementById("admin-panel-overlay");d!==null&&p(d)})}async function X(e,t,n){var u,m;const i=((u=n.tenantId)!=null?u:"").trim();if(i===""){window.alert("tenantId is required.");return}const a=M(t);if(a===null)return;const r=e.buildBody(n),o=t==="create"?e.endpoint:`${e.endpoint}/${encodeURIComponent(i)}`,s=await $(o,{method:t==="create"?"POST":"PUT",headers:{"Content-Type":"application/json",[E]:a},body:JSON.stringify(r)});if(!s.ok){window.alert(`Save failed (HTTP ${(m=s.status)!=null?m:"?"}).  See the panel for detail.`);const y=document.getElementById("admin-panel-overlay");y!==null&&p(y);return}const c=document.getElementById("admin-panel-overlay");c!==null&&p(c)}async function Q(e,t){var o;if(!window.confirm(`Delete the ${e.id} policy for "${t}"?  This cannot be undone.`))return;const i=M("delete");if(i===null)return;const a=await $(`${e.endpoint}/${encodeURIComponent(t)}`,{method:"DELETE",headers:{[E]:i}});!a.ok&&a.status!==204&&window.alert(`Delete failed (HTTP ${(o=a.status)!=null?o:"?"}).`);const r=document.getElementById("admin-panel-overlay");r!==null&&p(r)}export{te as openAdminPanel};
