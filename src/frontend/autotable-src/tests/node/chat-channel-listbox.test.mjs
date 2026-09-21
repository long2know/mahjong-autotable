import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const ts = require('typescript');
const javascript = ts.transpileModule(fs.readFileSync(path.join(root, 'src/ui/dark-listbox.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function fixture() {
  const observers = [];
  const document = Object.assign(new EventTarget(), { readyState: 'loading', activeElement: null });
  class Element extends EventTarget {
    children = [];
    parentNode = null;
    dataset = {};
    attributes = new Map();
    className = '';
    style = {};
    hidden = false;
    disabled = false;
    scrollTop = 0;
    text = '';
    classList = {
      contains: name => this.className.split(' ').includes(name),
      add: name => { if (!this.classList.contains(name)) this.className += ` ${name}`; },
    };
    set textContent(text) { this.text = text; this.children = []; }
    get textContent() { return this.text + this.children.map(child => child.textContent).join(''); }
    setAttribute(name, value) { this.attributes.set(name, value); }
    getAttribute(name) { return this.attributes.get(name) ?? null; }
    removeAttribute(name) { this.attributes.delete(name); }
    appendChild(child) {
      if (child.parentNode) child.parentNode.children = child.parentNode.children.filter(node => node !== child);
      child.parentNode = this;
      this.children.push(child);
      return child;
    }
    insertBefore(child, before) {
      child.parentNode = this;
      this.children.splice(this.children.indexOf(before), 0, child);
    }
    replaceChildren(...children) {
      this.textContent = '';
      children.forEach(child => this.appendChild(child));
    }
    contains(node) { return this === node || this.children.some(child => child.contains(node)); }
    matches(selector) {
      return selector.startsWith('#') ? this.id === selector.slice(1) : this.classList.contains(selector.slice(1));
    }
    closest(selector) { return this.matches(selector) ? this : this.parentNode?.closest(selector) ?? null; }
    querySelector(selector) {
      for (const child of this.children) {
        if (child.matches(selector)) return child;
        const nested = child.querySelector(selector);
        if (nested) return nested;
      }
      return null;
    }
    focus() { document.activeElement = this; }
    scrollIntoView() {}
    getBoundingClientRect() { return { left: 16, top: 140, right: 296, bottom: 180, width: 280, height: 40 }; }
    get scrollHeight() { return this.children.length * 40 + 10; }
    get clientHeight() { return this.scrollHeight; }
  }
  class Option extends Element {
    constructor(label, value) { super(); this.textContent = label; this.value = value; }
    get label() { return this.textContent; }
  }
  class OptGroup extends Element {}
  class Observer {
    constructor(callback) { this.callback = callback; observers.push(this); }
    observe(target, options) { this.target = target; this.options = options; }
  }
  document.createElement = () => new Element();
  document.documentElement = new Element();
  const window = Object.assign(new EventTarget(), { innerWidth: 390, innerHeight: 844 });
  const module = { exports: {} };
  runInNewContext(javascript, {
    document, window, Event, MutationObserver: Observer,
    HTMLOptionElement: Option, HTMLOptGroupElement: OptGroup,
    module, exports: module.exports, require: () => ({}),
  });
  const header = new Element();
  header.className = 'chat-header-selectors';
  const select = new Element();
  select.value = '';
  select.setAttribute('aria-label', 'Chat channel');
  header.appendChild(select);
  module.exports.enhanceDarkSelect(select);
  const trigger = header.querySelector('.dark-listbox-trigger');
  const popup = header.querySelector('.dark-listbox-popup');
  let changes = 0;
  select.addEventListener('change', () => { changes++; });
  function populate(labels = ['Table', 'Private'], selected = 'table') {
    select.replaceChildren(new Option(labels[0], 'table'), new Option(labels[1], 'private'));
    select.value = selected;
    for (const observer of observers.filter(observer => observer.target === select)) observer.callback([]);
  }
  function key(key) {
    const event = new Event('keydown', { cancelable: true });
    Object.defineProperty(event, 'key', { value: key });
    trigger.dispatchEvent(event);
  }
  return { select, trigger, popup, populate, key, changes: () => changes, document, observers };
}

test('an initially empty enhanced chat channel renders its selected label as soon as chat populates options', () => {
  const h = fixture();
  h.populate();
  assert.equal(h.trigger.textContent, 'Table');
  assert.equal(h.changes(), 0, 'rendering options must not choose a channel');
  const observer = h.observers.find(observer => observer.target === h.select);
  assert.equal(observer.options.childList, true);
  assert.equal(observer.options.subtree, true);
});

test('localized replacement options refresh a closed trigger without losing the selected private channel', () => {
  const h = fixture();
  h.populate();
  h.select.value = 'private';
  h.select.dispatchEvent(new Event('change'));
  h.populate(['牌桌', '私聊'], 'private');
  assert.equal(h.trigger.textContent, '私聊');
  assert.equal(h.select.value, 'private');
  assert.equal(h.changes(), 1);
  assert.equal(h.popup.hidden, true);
});

test('keyboard selection uses the refreshed options and fires the original native channel change exactly once', () => {
  const h = fixture();
  h.populate();
  h.trigger.focus();
  h.key('ArrowDown');
  assert.equal(h.popup.hidden, false);
  assert.equal(h.popup.children[0].textContent, 'Table');
  assert.equal(h.popup.children[1].textContent, 'Private');
  h.key('End');
  h.key('Enter');
  assert.equal(h.select.value, 'private');
  assert.equal(h.trigger.textContent, 'Private');
  assert.equal(h.changes(), 1);
  assert.equal(h.popup.hidden, true);
  assert.equal(h.document.activeElement, h.trigger);
});

test('an open channel menu refreshes localized text while retaining keyboard navigation, and Tab dismisses it', () => {
  const h = fixture();
  h.populate();
  h.key('ArrowDown');
  h.key('End');
  h.populate(['牌桌', '私聊']);
  assert.equal(h.popup.children[1].textContent, '私聊');
  assert.equal(h.popup.children[1].classList.contains('active'), true);
  h.key('Enter');
  assert.equal(h.select.value, 'private');
  h.key('ArrowDown');
  h.key('Tab');
  assert.equal(h.popup.hidden, true);
  assert.equal(h.trigger.getAttribute('aria-expanded'), 'false');
  assert.equal(h.changes(), 1);
});
