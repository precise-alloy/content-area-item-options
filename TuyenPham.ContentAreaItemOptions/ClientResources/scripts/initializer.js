define([
  // dojo
  'dojo/_base/declare',
  'dojo/aspect',

  // epi
  'epi/dependency',
  'epi/_Module',
  'epi/routes',
  'epi-cms/contentediting/editors/ContentAreaEditor',

  // custom
  'tuyen-pham/content-area-item-options/command/content-area-item-command',
], function (
  // dojo
  declare,
  aspect,

  // epi
  dependency,
  _Module,
  routes,
  ContentAreaEditor,

  // custom
  ContentAreaItemCommand,
) {
  return declare([_Module], {
    initialize: function () {
      this.inherited(arguments);

      var registry = dependency.resolve('epi.storeregistry');
      var store = registry.create('content-area-options', this._getRestPath('content-area-options'));
      var selectorsPromise = store.get();

      aspect.after(ContentAreaEditor.prototype, 'postCreate', function () {
        var editor = this;
        selectorsPromise.then(function (selectors) {
          for (var i = 0; i < selectors.length; i++) {
            var s = selectors[i];
            var cmd = new ContentAreaItemCommand({
              label: s.labelPrefix + ': ' + s.defaultLabel,
              attributeName: s.attributeName,
              labelPrefix: s.labelPrefix,
              defaultLabel: s.defaultLabel,
              availability: s.availability || 'All',
              preloadedOptions: s.options,
              preloadedRestrictions: s.restrictions,
            });
            editor.own(cmd);
            editor.add('commands', cmd);
          }
        });
      });
    },

    _getRestPath: function (name) {
      return routes.getRestPath({ moduleArea: 'TuyenPham.ContentAreaItemOptions', storeName: name });
    },
  });
});
