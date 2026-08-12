define([
  "dojo/_base/array",
  "dojo/_base/declare",
  "dojo/_base/lang",
  "epi/shell/DestroyableByKey",
  "epi-cms/widget/SelectorMenuBase",
  "epi/shell/widget/RadioMenuItem",
], function (array, declare, lang, DestroyableByKey, SelectorMenuBase, RadioMenuItem) {
  return declare([SelectorMenuBase, DestroyableByKey], {
    // summary:
    //      Radio menu that writes the selected option id into the content area
    //      item's render settings under `attributeName`.

    headingText: "",
    attributeName: "",
    defaultLabel: "Default",
    model: null,
    options: null,

    _rdDefault: null,

    // _applyingState: [private] Boolean
    //      True while checked state is being set programmatically, so the
    //      resulting "change" events do not write back to the model.
    _applyingState: false,

    onValueChange: function (/*String|null*/ value) {
      // summary:
      //      Called after the editor picks an option. The command overrides this
      //      to keep its label in sync; `attributes` is a plain object and cannot
      //      be watched via dojo/Stateful.
    },

    postCreate: function () {
      this.inherited(arguments);

      this.own(
        (this._rdDefault = new RadioMenuItem({
          label: this.defaultLabel,
          value: "",
        })),
      );
      this.addChild(this._rdDefault);
      this.own(
        this._rdDefault.on(
          "change",
          lang.hitch(this, function (checked) {
            // RadioMenuItem unchecks every sibling before checking the clicked
            // one, so only react when this item becomes the selected one.
            if (checked) {
              this._writeValue(null);
            }
          }),
        ),
      );
    },

    destroy: function () {
      this._removeMenuItems();
      this.inherited(arguments);
    },

    update: function (/*Object*/ model, /*Array*/ options) {
      // summary:
      //      Applies a new model and option list in one pass, so the menu is
      //      rebuilt once per block selection rather than once per setter.

      this._set("model", model);
      this._set("options", options);
      this._rebuildMenuItems();
    },

    _writeValue: function (/*String|null*/ value) {
      if (this._applyingState || !this.model) {
        return;
      }

      this.model.modify(function () {
        this.model.attributes[this.attributeName] = value;
      }, this);

      this.onValueChange(value);
    },

    _rebuildMenuItems: function () {
      if (!this.model || !this.options) {
        return;
      }

      this._removeMenuItems();

      var currentValue = this.model.attributes[this.attributeName];

      array.forEach(
        this.options,
        function (option) {
          var item = new RadioMenuItem({
            label: option.name,
            iconClass: option.iconClass || "",
            checked: currentValue === option.id,
            title: option.description || "",
          });

          // Watch is attached after construction, so the initial checked state
          // above never reaches _writeValue.
          this.ownByKey(
            "items",
            item.watch(
              "checked",
              lang.hitch(this, function (prop, oldVal, newVal) {
                if (newVal) {
                  this._writeValue(option.id);
                }
              }),
            ),
          );

          this.addChild(item);
        },
        this,
      );

      this._applyingState = true;
      this._rdDefault.set("checked", !currentValue);
      this._applyingState = false;
    },

    _removeMenuItems: function () {
      var children = this.getChildren();
      this.destroyByKey("items");
      array.forEach(
        children,
        function (child) {
          if (child === this._rdDefault) {
            return;
          }
          this.removeChild(child);
          child.destroy();
        },
        this,
      );
    },
  });
});
