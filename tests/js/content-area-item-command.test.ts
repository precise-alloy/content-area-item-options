import { beforeAll, describe, expect, test } from 'bun:test';
import { ContentBlockViewModel, loadAmdModule } from './amd-loader.js';

const MODULE_PATH =
  '../../TuyenPham.ContentAreaItemOptions/ClientResources/scripts/command/content-area-item-command.js';

let ContentAreaItemCommand: any;

const OPTIONS = [
  { id: 'black', name: 'Black' },
  { id: 'white', name: 'White' },
  { id: 'blue', name: 'Blue' },
];

function createCommand(overrides: Record<string, unknown> = {}) {
  return new ContentAreaItemCommand({
    attributeName: 'data-theme',
    labelPrefix: 'Theme',
    defaultLabel: 'Default',
    availability: 'All',
    options: OPTIONS,
    restrictions: {},
    contentAreaOverrides: null,
    ...overrides,
  });
}

function createModel(attributes: Record<string, unknown> = {}, contentTypeId = 1) {
  return new ContentBlockViewModel(attributes, contentTypeId);
}

function idsOf(options: Array<{ id: string }>) {
  return options.map((option) => option.id);
}

beforeAll(async () => {
  ContentAreaItemCommand = await loadAmdModule(MODULE_PATH);
});

// These mirror ContentAreaItemOptionsRestrictionResolverTests; the editor UI and the
// renderer must agree on which options are applicable.
describe('_getOptionsForModel precedence', () => {
  test('returns nothing without a model', () => {
    expect(createCommand()._getOptionsForModel()).toEqual([]);
  });

  test('Availability All shows every option when nothing restricts it', () => {
    const command = createCommand({ model: createModel() });

    expect(idsOf(command._getOptionsForModel())).toEqual(['black', 'white', 'blue']);
  });

  test('Availability Specific hides the selector without an opt-in', () => {
    const command = createCommand({ availability: 'Specific', model: createModel() });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('Availability None hides the selector unconditionally', () => {
    const command = createCommand({
      availability: 'None',
      model: createModel(),
      restrictions: { 1: [] },
      contentAreaOverrides: { 'data-theme': [] },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('content type restriction filters to the allowed ids', () => {
    const command = createCommand({
      model: createModel({}, 42),
      restrictions: { 42: ['black', 'white'] },
    });

    expect(idsOf(command._getOptionsForModel())).toEqual(['black', 'white']);
  });

  test('content type restriction with an empty list allows everything', () => {
    const command = createCommand({
      availability: 'Specific',
      model: createModel({}, 42),
      restrictions: { 42: [] },
    });

    expect(idsOf(command._getOptionsForModel())).toEqual(['black', 'white', 'blue']);
  });

  test('null content type restriction hides the selector', () => {
    const command = createCommand({
      model: createModel({}, 42),
      restrictions: { 42: null },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('unrestricted content type falls through to the property override', () => {
    const command = createCommand({
      availability: 'Specific',
      model: createModel({}, 7),
      restrictions: { 42: ['black'] },
      contentAreaOverrides: { 'data-theme': ['blue'] },
    });

    expect(idsOf(command._getOptionsForModel())).toEqual(['blue']);
  });

  test('property override with an empty list allows everything', () => {
    const command = createCommand({
      availability: 'Specific',
      model: createModel(),
      contentAreaOverrides: { 'data-theme': [] },
    });

    expect(idsOf(command._getOptionsForModel())).toEqual(['black', 'white', 'blue']);
  });

  test('null property override hides the selector', () => {
    const command = createCommand({
      model: createModel(),
      contentAreaOverrides: { 'data-theme': null },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('property override attribute names are case-insensitive', () => {
    const command = createCommand({
      model: createModel(),
      contentAreaOverrides: { 'DATA-THEME': null },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('property override for another selector is ignored', () => {
    const command = createCommand({
      availability: 'Specific',
      model: createModel(),
      contentAreaOverrides: { 'data-margin': [] },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('content type restriction wins over the property override', () => {
    const command = createCommand({
      model: createModel({}, 42),
      restrictions: { 42: null },
      contentAreaOverrides: { 'data-theme': [] },
    });

    expect(command._getOptionsForModel()).toEqual([]);
  });

  test('content type opt-in wins over a property hide', () => {
    const command = createCommand({
      model: createModel({}, 42),
      restrictions: { 42: ['black'] },
      contentAreaOverrides: { 'data-theme': null },
    });

    expect(idsOf(command._getOptionsForModel())).toEqual(['black']);
  });
});

describe('_refreshAvailability', () => {
  test('is unavailable when every option is filtered out', () => {
    const command = createCommand({
      model: createModel(),
      contentAreaOverrides: { 'data-theme': null },
      popup: new (class {
        updates: unknown[] = [];
        update(...args: unknown[]) {
          this.updates.push(args);
        }
      })(),
    });

    command._refreshAvailability();

    expect(command.isAvailable).toBe(false);
    expect(command.label).toBe('Theme: Default');
  });

  test('is unavailable when the model is not a content block', () => {
    const command = createCommand({ model: { attributes: {}, contentTypeId: 1 } });

    command._refreshAvailability();

    expect(command.isAvailable).toBe(false);
  });

  test('pushes the filtered options to the popup exactly once', () => {
    const popup = { updates: [] as unknown[], update(...args: unknown[]) { this.updates.push(args); } };
    const command = createCommand({
      model: createModel({}, 42),
      restrictions: { 42: ['black'] },
      popup,
    });

    command._refreshAvailability();

    expect(command.isAvailable).toBe(true);
    expect(popup.updates).toHaveLength(1);
    expect(idsOf((popup.updates[0] as [unknown, Array<{ id: string }>])[1])).toEqual(['black']);
  });

  test('labels the command with the stored option name', () => {
    const popup = { update() {} };
    const command = createCommand({
      model: createModel({ 'data-theme': 'blue' }),
      popup,
    });

    command._refreshAvailability();

    expect(command.label).toBe('Theme: Blue');
  });
});

describe('_updateLabel', () => {
  test('falls back to the default label for an empty value', () => {
    const command = createCommand();

    command._updateLabel(null);

    expect(command.label).toBe('Theme: Default');
  });

  test('falls back to the default label for an option that no longer exists', () => {
    const command = createCommand();

    command._updateLabel('removed-option');

    expect(command.label).toBe('Theme: Default');
  });

  test('uses the option name for a known value', () => {
    const command = createCommand();

    command._updateLabel('white');

    expect(command.label).toBe('Theme: White');
  });
});

describe('_onModelChange', () => {
  test('marks the command unavailable when the model is cleared', () => {
    const command = createCommand({ isAvailable: true });

    command._onModelChange();

    expect(command.isAvailable).toBe(false);
    expect(command.canExecute).toBe(false);
  });
});
