define(['dojo/_base/declare', 'dojo/_base/lang', 'dojo/request/xhr', 'epi-cms/contentediting/command/_ContentAreaCommand', 'epi-cms/contentediting/viewmodel/ContentBlockViewModel', 'tuyen-pham/content-area-item-options/widget/content-area-item-selector'], function (declare, lang, xhr, _ContentAreaCommand, ContentBlockViewModel, ContentAreaItemSelector) {
  return declare([_ContentAreaCommand], {
    // Override these in subclass or constructor params
    label: 'Option: Default',
    category: 'popup',
    attributeName: '',
    apiUrl: '',
    labelPrefix: 'Option',
    defaultLabel: 'Default',
    availability: 'All',

    // Pass preloadedOptions and preloadedRestrictions to skip the API call
    preloadedOptions: null,
    preloadedRestrictions: null,

    _allOptions: null,
    _restrictions: null,

    postscript: function () {
      this.inherited(arguments);

      this.popup = new ContentAreaItemSelector({
        headingText: this.labelPrefix,
        attributeName: this.attributeName,
        defaultLabel: this.defaultLabel,
      });

      if (this.preloadedOptions) {
        this._allOptions = this.preloadedOptions;
        this._restrictions = this.preloadedRestrictions || {};
        this._refreshAvailability();
      } else if (this.apiUrl) {
        xhr(this.apiUrl, {
          handleAs: 'json',
          headers: { Accept: 'application/json' },
        }).then(
          lang.hitch(this, function (response) {
            this._allOptions = response.options || [];
            this._restrictions = response.restrictions || {};
            this._refreshAvailability();
          }),
          lang.hitch(this, function () {
            this._allOptions = [];
            this._restrictions = {};
            this.set('isAvailable', false);
          }),
        );
      }
    },

    _getOptionsForModel: function () {
      var options = this._allOptions;
      if (!options || !this.model) {
        return options;
      }

      var contentTypeId = this.model.contentTypeId;
      if (contentTypeId && this._restrictions.hasOwnProperty(contentTypeId)) {
        var allowed = this._restrictions[contentTypeId];
        if (!allowed || allowed.length === 0) {
          return [];
        }
        return options.filter(function (opt) {
          return allowed.indexOf(opt.id) >= 0;
        });
      }

      // Specific mode: hide for content types without an explicit attribute
      if (this.availability === 'Specific') {
        return [];
      }

      return options;
    },

    _refreshAvailability: function () {
      var options = this._getOptionsForModel();
      var isAvailable = options && options.length > 0 && this.model instanceof ContentBlockViewModel;

      this.set('isAvailable', isAvailable);

      if (isAvailable) {
        this.popup.set('model', this.model);
        this.popup.set('options', options);

        var selectedValue = this.model.attributes[this.attributeName];
        if (!selectedValue) {
          this.set('label', this.labelPrefix + ': ' + this.defaultLabel);
        } else {
          this._updateLabel(selectedValue);
        }
      }
    },

    destroy: function () {
      this.inherited(arguments);
      if (this.popup) {
        this.popup.destroyRecursive();
      }
    },

    _onModelChange: function () {
      if (!this.model) {
        this.set('isAvailable', false);
        return;
      }

      this.inherited(arguments);

      this._refreshAvailability();

      if (!this.get('isAvailable')) {
        return;
      }

      this._watch(
        this.attributeName,
        function (prop, oldVal, newVal) {
          if (!newVal) {
            this.set('label', this.labelPrefix + ': ' + this.defaultLabel);
          } else {
            this._updateLabel(newVal);
          }
        },
        this,
      );
    },

    _updateLabel: function (optionId) {
      var options = this._allOptions;
      if (options) {
        for (var i = 0; i < options.length; i++) {
          if (options[i].id === optionId) {
            this.set('label', this.labelPrefix + ': ' + options[i].name);
            return;
          }
        }
      }
      this.set('label', this.labelPrefix + ': ' + this.defaultLabel);
    },

    _onModelValueChange: function () {
      this.set('canExecute', !!this.model && (this.model.contentLink || this.model.inlineBlockData) && !this.model.get('readOnly'));
    },
  });
});
