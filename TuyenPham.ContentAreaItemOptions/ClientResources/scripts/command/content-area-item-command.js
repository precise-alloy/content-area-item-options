define([
  "dojo/_base/declare",
  "dojo/_base/lang",
  "epi-cms/contentediting/command/_ContentAreaCommand",
  "epi-cms/contentediting/viewmodel/ContentBlockViewModel",
  "tuyen-pham/content-area-item-options/widget/content-area-item-selector",
], function (declare, lang, _ContentAreaCommand, ContentBlockViewModel, ContentAreaItemSelector) {
  return declare([_ContentAreaCommand], {
    // summary:
    //      Context menu command for a single selector. Mirrors the server-side
    //      ContentAreaItemOptionsRestrictionResolver precedence:
    //      content type > ContentArea property > selector availability.

    label: "Option: Default",
    category: "popup",

    attributeName: "",
    labelPrefix: "Option",
    defaultLabel: "Default",
    availability: "All",

    // options: [public] Array
    //      Every option defined for this selector, before filtering.
    options: null,

    // restrictions: [public] Object
    //      contentTypeId -> allowed option ids. null = hidden, [] = all allowed.
    restrictions: null,

    // contentAreaOverrides: [public] Object
    //      attributeName -> allowed option ids, from [ContentAreaItemOptions] or
    //      [HideContentAreaItemOptions] on the ContentArea property.
    contentAreaOverrides: null,

    postscript: function () {
      this.inherited(arguments);

      this.options = this.options || [];
      this.restrictions = this.restrictions || {};

      this.popup = new ContentAreaItemSelector({
        headingText: this.labelPrefix,
        attributeName: this.attributeName,
        defaultLabel: this.defaultLabel,
        onValueChange: lang.hitch(this, this._updateLabel),
      });

      this._updateLabel(null);
    },

    destroy: function () {
      this.inherited(arguments);

      if (this.popup) {
        this.popup.destroyRecursive();
      }
    },

    _onModelChange: function () {
      // The base implementation resets canExecute and releases the previous
      // model's watch handles when the model is cleared.
      this.inherited(arguments);

      if (!this.model) {
        this.set("isAvailable", false);
        return;
      }

      this._refreshAvailability();
    },

    _onModelValueChange: function () {
      this.set(
        "canExecute",
        !!this.model &&
          (this.model.contentLink || this.model.inlineBlockData) &&
          !this.model.get("readOnly"),
      );
    },

    _refreshAvailability: function () {
      var options = this._getOptionsForModel();
      var isAvailable = options.length > 0 && this.model instanceof ContentBlockViewModel;

      this.set("isAvailable", isAvailable);

      if (!isAvailable) {
        this._updateLabel(null);
        return;
      }

      this.popup.update(this.model, options);
      this._updateLabel(this.model.attributes[this.attributeName]);
    },

    _getOptionsForModel: function () {
      if (!this.model) {
        return [];
      }

      // None is unconditionally hidden; no attribute can opt back in.
      if (this.availability === "None") {
        return [];
      }

      var contentTypeId = this.model.contentTypeId;
      if (contentTypeId && this.restrictions.hasOwnProperty(contentTypeId)) {
        return this._filter(this.restrictions[contentTypeId]);
      }

      if (this.contentAreaOverrides && this.contentAreaOverrides.hasOwnProperty(this.attributeName)) {
        return this._filter(this.contentAreaOverrides[this.attributeName]);
      }

      return this.availability === "Specific" ? [] : this.options;
    },

    _filter: function (/*Array|null*/ allowedIds) {
      // null = selector hidden; empty = every option allowed.
      if (allowedIds === null || allowedIds === undefined) {
        return [];
      }

      if (allowedIds.length === 0) {
        return this.options;
      }

      return this.options.filter(function (option) {
        return allowedIds.indexOf(option.id) >= 0;
      });
    },

    _updateLabel: function (/*String|null*/ optionId) {
      var name = this.defaultLabel;

      if (optionId) {
        for (var i = 0; i < this.options.length; i++) {
          if (this.options[i].id === optionId) {
            name = this.options[i].name;
            break;
          }
        }
      }

      this.set("label", this.labelPrefix + ": " + name);
    },
  });
});
