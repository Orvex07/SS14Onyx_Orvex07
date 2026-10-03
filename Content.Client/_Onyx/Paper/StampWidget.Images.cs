// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client.Paper.UI;

public sealed partial class StampWidget
{
    private static readonly ResPath LegacyStampImageRoot = new("/Textures/_Onyx/Interface/Paper/Stamps");

    private bool TrySetStampImage(StampDisplayInfo stampInfo)
    {
        if (stampInfo.StampLargeIcon is not { } configuredPath)
            return false;

        var imagePath = new ResPath(configuredPath);
        if (imagePath.Directory is var directory && (directory.IsSelf || directory == ResPath.Root))
            imagePath = LegacyStampImageRoot / new ResPath(imagePath.Filename).WithExtension("png");

        var resourceCache = IoCManager.Resolve<IResourceCache>();
        if (!resourceCache.TryGetResource<TextureResource>(imagePath, out var image))
            return false;

        PanelOverride = new StyleBoxTexture { Texture = image };
        SetSize = new Vector2(image.Texture.Width * 1.5f, image.Texture.Height * 1.5f);
        return true;
    }
}
