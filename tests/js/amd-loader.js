// Minimal AMD loader so the Dojo modules under ClientResources can be exercised
// in isolation. Only the members the modules actually touch are stubbed.

function declare(bases, props) {
  const baseList = Array.isArray(bases) ? bases : [bases];
  const Base = baseList[0];

  function Ctor(params) {
    Object.assign(this, params);
  }

  Ctor.prototype = Object.create(Base ? Base.prototype : Object.prototype);
  Ctor.prototype.constructor = Ctor;

  // Methods are wrapped so this.inherited(arguments) reaches the base implementation,
  // which is how the real dojo/_base/declare behaves.
  for (const [name, value] of Object.entries(props)) {
    if (typeof value !== "function") {
      Ctor.prototype[name] = value;
      continue;
    }

    const inheritedImpl = Base && Base.prototype[name];

    Ctor.prototype[name] = function (...args) {
      const previous = this.inherited;
      this.inherited = () => (inheritedImpl ? inheritedImpl.apply(this, args) : undefined);
      try {
        return value.apply(this, args);
      } finally {
        this.inherited = previous;
      }
    };
  }

  return Ctor;
}

class Stateful {
  set(name, value) {
    this[name] = value;
  }

  get(name) {
    return this[name];
  }

  inherited() {}

  own() {}
}

class ContentAreaCommand extends Stateful {
  _onModelChange() {
    if (!this.model) {
      this.set("canExecute", false);
    }
  }

  _onModelValueChange() {}
}

class ContentBlockViewModel {
  constructor(attributes, contentTypeId) {
    this.attributes = attributes || {};
    this.contentTypeId = contentTypeId;
  }

  get(name) {
    return this[name];
  }
}

class ContentAreaItemSelector {
  constructor(params) {
    Object.assign(this, params);
    this.updates = [];
  }

  update(model, options) {
    this.updates.push({ model, options });
  }

  destroyRecursive() {}
}

const modules = {
  "dojo/_base/declare": declare,
  "dojo/_base/lang": {
    hitch: (scope, fn) => fn.bind(scope),
  },
  "epi-cms/contentediting/command/_ContentAreaCommand": ContentAreaCommand,
  "epi-cms/contentediting/viewmodel/ContentBlockViewModel": ContentBlockViewModel,
  "tuyen-pham/content-area-item-options/widget/content-area-item-selector": ContentAreaItemSelector,
};

export async function loadAmdModule(path) {
  let exported;

  globalThis.define = (dependencies, factory) => {
    exported = factory(
      ...dependencies.map((name) => {
        if (!(name in modules)) {
          throw new Error(`No stub registered for AMD dependency "${name}"`);
        }
        return modules[name];
      }),
    );
  };

  await import(path);
  delete globalThis.define;

  return exported;
}

export { ContentBlockViewModel };
