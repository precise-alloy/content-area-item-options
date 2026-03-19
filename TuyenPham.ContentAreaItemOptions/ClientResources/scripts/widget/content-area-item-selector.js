define([
    "dojo/_base/array",
    "dojo/_base/declare",
    "dojo/_base/lang",
    "epi/shell/DestroyableByKey",
    "epi-cms/widget/SelectorMenuBase",
    "epi/shell/widget/RadioMenuItem",
], function (
    array,
    declare,
    lang,
    DestroyableByKey,
    SelectorMenuBase,
    RadioMenuItem,
) {
    return declare([SelectorMenuBase, DestroyableByKey], {
        headingText: "",
        attributeName: "",
        defaultLabel: "Default",
        model: null,
        options: null,
        _rdDefault: null,

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
                    lang.hitch(this, function () {
                        if (this.model) {
                            this.model.modify(function () {
                                this.model.attributes[this.attributeName] = null;
                            }, this);
                        }
                    }),
                ),
            );
        },

        _setModelAttr: function (model) {
            this._set("model", model);
            this._setup();
        },

        _setOptionsAttr: function (options) {
            this._set("options", options);
            this._setup();
        },

        _setup: function () {
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

                    this.ownByKey(
                        "items",
                        item.watch(
                            "checked",
                            lang.hitch(this, function (prop, oldVal, newVal) {
                                if (!newVal) {
                                    return;
                                }
                                this.model.modify(function () {
                                    this.model.attributes[this.attributeName] = option.id;
                                }, this);
                            }),
                        ),
                    );

                    this.addChild(item);
                },
                this,
            );

            this._rdDefault.set("checked", !currentValue);
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
