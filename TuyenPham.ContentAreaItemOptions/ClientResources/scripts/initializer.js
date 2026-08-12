define([
  // dojo
  "dojo/_base/declare",
  "dojo/aspect",

  // epi
  "epi/dependency",
  "epi/_Module",
  "epi/routes",
  "epi-cms/contentediting/editors/ContentAreaEditor",

  // custom
  "tuyen-pham/content-area-item-options/command/content-area-item-command",
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
  // Namespaced so it cannot collide with another module's store; epi's registry
  // throws when a name is taken.
  var STORE_KEY = "tuyen-pham.content-area-item-options";
  var STORE_NAME = "content-area-options";

  return declare([_Module], {
    initialize: function () {
      this.inherited(arguments);

      var registry = dependency.resolve("epi.storeregistry");
      var store = registry.create(
        STORE_KEY,
        routes.getRestPath({
          moduleArea: "TuyenPham.ContentAreaItemOptions",
          storeName: STORE_NAME,
        }),
      );

      // Resolved once per session; a failure degrades to "no selectors" rather
      // than leaving every content area editor waiting forever.
      var selectorsPromise = store.get().then(
        function (selectors) {
          return selectors || [];
        },
        function (error) {
          console.error("[content-area-item-options] failed to load selectors", error);
          return [];
        },
      );

      aspect.after(ContentAreaEditor.prototype, "postCreate", function () {
        var editor = this;

        // Set from EditorConfiguration["contentAreaItemOptions"] by
        // ContentAreaItemOptionsMetadataExtender when the ContentArea property
        // is decorated. Widget params are mixed in before postCreate runs.
        var contentAreaOverrides = editor.contentAreaItemOptions || null;

        selectorsPromise.then(function (selectors) {
          selectors.forEach(function (selector) {
            var command = new ContentAreaItemCommand({
              attributeName: selector.attributeName,
              labelPrefix: selector.labelPrefix,
              defaultLabel: selector.defaultLabel,
              availability: selector.availability || "All",
              options: selector.options,
              restrictions: selector.restrictions,
              contentAreaOverrides: contentAreaOverrides,
            });

            if (editor._destroyed) {
              command.destroy();
              return;
            }

            editor.own(command);
            editor.add("commands", command);
          });
        });
      });
    },
  });
});
