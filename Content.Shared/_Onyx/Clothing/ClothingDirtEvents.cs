// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared._Onyx.Clothing;

public readonly record struct PuddleDirtChangedEvent;

public readonly record struct BodyPartVisualChangedEvent;

[ByRefEvent]
public record struct BleedingDirtEvent(Solution Source, FixedPoint2 Amount, bool Handled = false);
