using System;
using System.Collections.Generic;
using Godot;

public static class MatchSpritePresentationTests
{
    public static void Run()
    {
        VerifyFacingStrideAndTeleport();
        VerifyKitUsesTeamData();
        VerifyAtlasAndRenderer();
        VerifyExportedSheetsMatchTheirSources();
        GD.Print("PASS: sprite 8 hướng, mask trang phục, đổi áo, bước chạy và bóng dùng đúng presentation.");
    }

    private static void VerifyFacingStrideAndTeleport()
    {
        Vector2[] directions =
        {
            new(0f, 1f), new(-1f, 1f), new(-1f, 0f), new(-1f, -1f),
            new(0f, -1f), new(1f, -1f), new(1f, 0f), new(1f, 1f)
        };
        for (int facing = 0; facing < 8; facing++)
        {
            Check(MatchSpriteMotion.FacingFromTravel(directions[facing]) == facing,
                "Di chuyển phải chọn đúng một trong 8 góc nhìn.");
        }
        MatchSpriteMotion motion = new(new Vector2(0.5f, 0.5f), 2);
        motion.Capture(new Vector2(0.505f, 0.5f), 2);
        Check(motion.Facing == 6 && motion.IsMoving && motion.StridePhase > 0f,
            "Chạy sang phải phải quay mặt sang phải và tiến nhịp bước.");
        float phase = motion.StridePhase;
        motion.Capture(new Vector2(0.505f, 0.5f), 2);
        Check(!motion.IsMoving && motion.StridePhase == phase && motion.Facing == 6,
            "Dừng tại chỗ phải giữ hướng và ngừng tiến nhịp animation.");
        motion.Capture(new Vector2(0.8f, 0.5f), 2);
        Check(motion.Facing == 2 && motion.StridePhase == 0f && !motion.IsMoving,
            "Teleport/scenario reset không được tính thành quãng đường chạy.");
    }

    private static void VerifyKitUsesTeamData()
    {
        FootballTeam team = new FootballTeam().setup("kit", "Kit Team", "KIT", "VN",
            new Color("1234dc"), new Color("fcf2dd"));
        MatchSpriteKitPalette home = MatchSpriteKitPalette.ForTeam(team, false);
        MatchSpriteKitPalette away = MatchSpriteKitPalette.ForTeam(team, true);
        Check(home.Shirt == team.primary_color && home.Shorts == team.secondary_color &&
            away.Shirt == team.secondary_color && away.Shorts == team.primary_color,
            "Áo sân nhà/sân khách phải lấy từ dữ liệu đội, không gắn vào ảnh sprite.");
        team.primary_color = new Color("f42c18");
        Check(MatchSpriteKitPalette.ForTeam(team, false).Shirt == team.primary_color,
            "Thay màu đội phải áp dụng được mà không thay sprite gốc.");
        team.Dispose();
    }

    private static void VerifyAtlasAndRenderer()
    {
        MatchSpriteAtlas atlas = new();
        Check(atlas.PlayerSheet.GetSize() == new Vector2(192f, 144f) &&
            atlas.KitMaskSheet.GetSize() == atlas.PlayerSheet.GetSize(),
            "Sheet cầu thủ và sheet mask phải là lưới 4 × 2 ô 48 × 72.");
        for (int facing = 0; facing < 8; facing++)
        {
            Check(atlas.Player(facing) is AtlasTexture frame && frame.Atlas == atlas.PlayerSheet &&
                frame.Region == (Rect2)MatchSpriteAtlas.FrameRegion(facing),
                "Mỗi hướng phải là một vùng của sheet đã xuất, không phải ảnh cắt lại lúc mở game.");
            Check(atlas.Player(facing).GetSize() == new Vector2(48f, 72f) &&
                atlas.Mask(facing).GetSize() == atlas.Player(facing).GetSize(),
                "Mọi hướng phải dùng cùng kích thước và mask trùng pixel.");
            using Image mask = atlas.Mask(facing).GetImage();
            using Image sprite = atlas.Player(facing).GetImage();
            int shirt = 0;
            int shorts = 0;
            int socks = 0;
            for (int y = 0; y < mask.GetHeight(); y++)
            {
                for (int x = 0; x < mask.GetWidth(); x++)
                {
                    Color region = mask.GetPixel(x, y);
                    Check(region.A > 0f || region.R + region.G + region.B == 0f,
                        "Pixel trong suốt của mask không được mang màu, nếu không shader sẽ tô nhầm viền.");
                    if (region.R > 0.9f) shirt++;
                    if (region.G > 0.9f) shorts++;
                    if (region.B > 0.9f) socks++;
                    Color source = sprite.GetPixel(x, y);
                    if (y < 24 || source.R - source.B > 0.15f || y >= 64)
                    {
                        Check(region.A == 0f,
                            "Mask không được đổi màu đầu, da hoặc phần giày dưới chân.");
                    }
                }
            }
            Check(shirt > 10 && shorts > 5 && socks > 2,
                "Mỗi hướng phải có vùng áo, quần và tất để đổi màu riêng.");
        }
        Check(atlas.Ball(0).GetSize() == new Vector2(16f, 16f), "Bóng phải có sprite riêng.");
        Check(atlas.BallShadow.GetSize() == new Vector2(16f, 8f), "Bóng đổ phải có sprite pixel riêng.");

        MatchSpriteRenderer renderer = new();
        renderer.Initialize(atlas);
        Dictionary<StringName, Vector2> positions = new() { ["home"] = new(0.5f, 0.5f) };
        Dictionary<StringName, StringName> teams = new() { ["home"] = "team" };
        renderer.Reset(positions, teams, "team", true, new Vector2(0.5f, 0.5f));
        MatchSpriteKitPalette blue = new(new Color("2464dd"), Colors.White, new Color("2464dd"));
        MatchSpriteKitPalette red = new(new Color("e42e2e"), Colors.Black, Colors.White);
        Vector2 foot = new(350f, 260f);
        renderer.SetPlayerVisual("home", foot, 32f, blue, PlayerMarkerLabelMode.SquadNumber, "CM", 8);
        Sprite2D player = renderer.GetNode<Sprite2D>("Player_home");
        Texture2D texture = player.Texture;
        ShaderMaterial material = (ShaderMaterial)player.Material;
        Check(material.GetShaderParameter("kit_mask").As<Texture2D>() == atlas.KitMaskSheet &&
            material.GetShaderParameter("sheet_grid").AsVector2() == new Vector2(4f, 2f),
            "Shader phải nhận cả sheet mask và kích thước lưới để lấy đúng ô của từng hướng.");
        renderer.SetPlayerVisual("home", foot, 32f, red, PlayerMarkerLabelMode.Position, "CM", 8);
        Check(player.Texture == texture && player.Position == foot &&
            material.GetShaderParameter("shirt_color").AsColor() == red.Shirt &&
            renderer.GetNode<Label>("Label_home").Text == "CM",
            "Đổi áo và nhãn phải giữ nguyên sprite/điểm chân và cập nhật material riêng.");
        positions["home"] = new Vector2(0.505f, 0.5f);
        renderer.Capture(positions, teams, "team", true, new Vector2(0.505f, 0.5f));
        renderer.SetPlayerVisual("home", foot, 32f, red, PlayerMarkerLabelMode.Position, "CM", 8);
        Check(player.Texture == atlas.Player(6), "Sprite trên renderer phải quay sang hướng di chuyển.");
        float step = material.GetShaderParameter("step_offset").AsSingle();
        renderer.Capture(positions, teams, "team", true, new Vector2(0.505f, 0.5f), false);
        renderer.SetPlayerVisual("home", foot, 32f, red, PlayerMarkerLabelMode.Position, "CM", 8);
        Check(material.GetShaderParameter("step_offset").AsSingle() == step,
            "Render frame/pause không có simulation tick mới phải giữ nguyên pose.");
        renderer.SetPlayerMarkerVisual("home", foot, 24f, red.Shirt, "8", 400);
        Check(player.Material is null && player.ZIndex == 400,
            "Chấm cầu thủ cũng phải nằm trong cùng lớp sắp xếp với gôn.");
        renderer.SetPlayerVisual("home", foot, 32f, red, PlayerMarkerLabelMode.Position, "CM", 8, 401);
        Check(player.Material == material && player.Texture == atlas.Player(6) &&
            player.Offset == -MatchSpriteAtlas.FootPivot && player.ZIndex == 401 &&
            renderer.GetNode<Label>("Label_home").Text == "CM",
            "Bật sprite lại phải phục hồi pose, điểm chân, nhãn và material.");
        renderer.SetBallVisual(foot, foot - new Vector2(0f, 20f), 10f, true);
        Sprite2D ball = renderer.GetNode<Sprite2D>("BallSprite");
        Sprite2D ballShadow = renderer.GetNode<Sprite2D>("BallShadowSprite");
        Check(ball.Visible && ballShadow.Visible && ball.Position.Y < foot.Y && ball.ZIndex > player.ZIndex && ball.ZIndex > ballShadow.ZIndex,
            "Bóng bổng phải ở phía trên bóng đổ và cầu thủ.");
        renderer.SetBallVisual(foot, foot, 10f, false);
        Check(!ball.Visible && !ballShadow.Visible, "Bóng bị ẩn trong engine phải ẩn sprite tương ứng.");
        renderer.Free();
    }

    // The committed sheets must be exactly what the offline exporter produces from the source boards.
    private static void VerifyExportedSheetsMatchTheirSources()
    {
        MatchSpriteAtlas atlas = new();
        using PlayerSpriteSheetBuilder.Sheets expected = PlayerSpriteSheetBuilder.Build();
        Check(PlayerSpriteSheetBuilder.HasSamePixels(expected.Players, atlas.PlayerSheet.GetImage()),
            "Sheet cầu thủ đã xuất không khớp ảnh nguồn; hãy chạy lại PlayerSpriteSheetExporter.");
        Check(PlayerSpriteSheetBuilder.HasSamePixels(expected.KitMask, atlas.KitMaskSheet.GetImage()),
            "Sheet mask đã xuất không khớp ảnh nguồn; hãy chạy lại PlayerSpriteSheetExporter.");
        Check(PlayerSpriteSheetBuilder.HasSamePixels(expected.Ball, atlas.BallSheet.GetImage()),
            "Sheet bóng đã xuất không khớp ảnh nguồn; hãy chạy lại PlayerSpriteSheetExporter.");
        Check(PlayerSpriteSheetBuilder.HasSamePixels(expected.BallShadow, atlas.BallShadow.GetImage()),
            "Sheet bóng đổ đã xuất không khớp; hãy chạy lại PlayerSpriteSheetExporter.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
