using System;
using System.Linq;
using Godot;

// Writes the normalised match sprite sheets to assets/. Run after changing a source board:
//   Godot --headless --path . res://tools/assets/PlayerSpriteSheetExporter.tscn
// Add "-- --check" to verify that the committed sheets still match their sources without writing.
public partial class PlayerSpriteSheetExporter : Node
{
    public override void _Ready()
    {
        Callable.From(Run).CallDeferred();
    }

    private void Run()
    {
        try
        {
            bool checkOnly = OS.GetCmdlineUserArgs().Contains("--check");
            using PlayerSpriteSheetBuilder.Sheets sheets = PlayerSpriteSheetBuilder.Build();
            bool upToDate = true;
            upToDate &= Process(sheets.Players, MatchSpriteAtlas.PlayerSheetPath, checkOnly);
            upToDate &= Process(sheets.KitMask, MatchSpriteAtlas.KitMaskSheetPath, checkOnly);
            upToDate &= Process(sheets.Ball, MatchSpriteAtlas.BallSheetPath, checkOnly);
            upToDate &= Process(sheets.BallShadow, MatchSpriteAtlas.BallShadowPath, checkOnly);
            if (!checkOnly)
            {
                ExportFrames(sheets);
            }
            if (checkOnly && !upToDate)
            {
                GD.PushError("SPRITE_SHEET_STALE: run the exporter without --check and commit the result.");
                GetTree().Quit(1);
                return;
            }
            GD.Print(checkOnly ? "PASS: exported sprite sheets match their sources." : "SPRITE_SHEET_EXPORT_COMPLETE");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SPRITE_SHEET_EXPORT_FAILED {exception}");
            GetTree().Quit(2);
        }
    }

    private static void ExportFrames(PlayerSpriteSheetBuilder.Sheets sheets)
    {
        string baseDir = ProjectSettings.GlobalizePath("res://assets/football/pixel_sprites/runtime/frames");
        DirAccess.MakeDirRecursiveAbsolute(baseDir);

        string[] facingNames = { "0_down", "1_down_left", "2_left", "3_up_left", "4_up", "5_up_right", "6_right", "7_down_right" };
        for (int i = 0; i < MatchSpriteAtlas.FacingCount; i++)
        {
            Rect2I region = MatchSpriteAtlas.FrameRegion(i);
            using Image playerFrame = sheets.Players.GetRegion(region);
            using Image maskFrame = sheets.KitMask.GetRegion(region);
            playerFrame.SavePng($"{baseDir}/player_{facingNames[i]}.png");
            maskFrame.SavePng($"{baseDir}/mask_{facingNames[i]}.png");
        }

        for (int frame = 0; frame < MatchSpriteAtlas.BallFrameCount; frame++)
        {
            using Image ballFrame = sheets.Ball.GetRegion(new Rect2I(frame * MatchSpriteAtlas.BallSize, 0, MatchSpriteAtlas.BallSize, MatchSpriteAtlas.BallSize));
            ballFrame.SavePng($"{baseDir}/ball_{frame}.png");
        }
        sheets.BallShadow.SavePng($"{baseDir}/ball_shadow.png");
        GD.Print($"WROTE individual frames to {baseDir}");
    }

    private static bool Process(Image sheet, string resourcePath, bool checkOnly)
    {
        string filePath = ProjectSettings.GlobalizePath(resourcePath);
        if (checkOnly)
        {
            bool matches = FileAccess.FileExists(resourcePath) && PlayerSpriteSheetBuilder.HasSamePixels(sheet, Image.LoadFromFile(filePath));
            GD.Print($"{(matches ? "OK   " : "STALE")} {resourcePath}");
            return matches;
        }

        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(filePath.GetBaseDir());
        if (directoryError != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot create {filePath.GetBaseDir()}: {directoryError}");
        }
        Error saveError = sheet.SavePng(filePath);
        if (saveError != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot write {filePath}: {saveError}");
        }
        GD.Print($"WROTE {resourcePath} ({sheet.GetWidth()} x {sheet.GetHeight()})");
        return true;
    }
}
