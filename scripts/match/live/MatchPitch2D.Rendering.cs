using Godot;

public partial class MatchPitch2D
{
    public override void _Draw()
    {
        if (Simulation is null)
            return;
        Rect2 field = CalculateFieldRect(Size);
        DrawRect(field, new Color("176b45"));
        float stripeWidth = field.Size.X / 12f;
        for (int i = 0; i < 12; i++)
        {
            if (i % 2 == 0)
                DrawRect(new Rect2(field.Position + new Vector2(stripeWidth * i, 0), new Vector2(stripeWidth, field.Size.Y)), new Color("1c764e"));
        }

        Color line = new(1, 1, 1, 0.66f);
        Vector2 center = field.GetCenter();
        DrawRect(field, line, false, 2);
        DrawLine(new Vector2(center.X, field.Position.Y), new Vector2(center.X, field.End.Y), line, 2);
        float centerCircleRadius = field.Size.Y *
            FootballPitchDimensions.CenterCircleRadiusMeters /
            FootballPitchDimensions.WidthMeters;
        DrawArc(center, centerCircleRadius, 0, Mathf.Tau, 48, line, 2);
        DrawCircle(center, 3, line);

        float penaltyWidth = field.Size.X *
            FootballPitchDimensions.PenaltyAreaDepthMeters /
            FootballPitchDimensions.LengthMeters;
        float penaltyHeight = field.Size.Y *
            FootballPitchDimensions.PenaltyAreaWidthMeters /
            FootballPitchDimensions.WidthMeters;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - penaltyWidth, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);

        float goalAreaWidth = field.Size.X *
            FootballPitchDimensions.GoalAreaDepthMeters /
            FootballPitchDimensions.LengthMeters;
        float goalAreaHeight = field.Size.Y *
            FootballPitchDimensions.GoalAreaWidthMeters /
            FootballPitchDimensions.WidthMeters;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - goalAreaWidth, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);

        float penaltySpotOffset = field.Size.X *
            FootballPitchDimensions.PenaltySpotDistanceMeters /
            FootballPitchDimensions.LengthMeters;
        Vector2 leftSpot = new(field.Position.X + penaltySpotOffset, center.Y);
        Vector2 rightSpot = new(field.End.X - penaltySpotOffset, center.Y);
        DrawCircle(leftSpot, 2.5f, line);
        DrawCircle(rightSpot, 2.5f, line);
        Vector2 arcRadius = new(
            field.Size.X * FootballPitchDimensions.CenterCircleRadiusMeters / FootballPitchDimensions.LengthMeters,
            field.Size.Y * FootballPitchDimensions.CenterCircleRadiusMeters / FootballPitchDimensions.WidthMeters);
        DrawEllipticalArc(leftSpot, arcRadius, -0.93f, 0.93f, line);
        DrawEllipticalArc(rightSpot, arcRadius, Mathf.Pi - 0.93f, Mathf.Pi + 0.93f, line);

        float goalHeight = field.Size.Y *
            FootballPitchDimensions.GoalWidthMeters /
            FootballPitchDimensions.WidthMeters;
        DrawRect(new Rect2(new Vector2(field.Position.X - 8, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);

        DrawPlayers(field);
        DrawBall(field);
    }

    private void DrawPlayers(Rect2 field)
    {
        if (Simulation is null)
        {
            return;
        }

        float playerRadius = Mathf.Clamp(field.Size.Y * 0.021f, 9f, 14f);
        int fontSize = Mathf.RoundToInt(Mathf.Clamp(playerRadius * 0.92f, 9f, 13f));
        foreach ((StringName playerId, Vector2 normalized) in CurrentPositions)
        {
            Vector2 point = ToFieldPoint(normalized, field);
            bool isHome = _playerTeams[playerId] == Simulation.home.team.id;
            Color color = isHome ? HomeColor : AwayColor;
            if (_playerRoles[playerId] == "GK")
                color = isHome ? new Color("f1c75b") : new Color("ec9f45");
            DrawCircle(point + new Vector2(1.5f, 2), playerRadius + 0.5f, new Color(0, 0, 0, 0.32f));
            DrawCircle(point, playerRadius, color);
            DrawArc(point, playerRadius, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.84f), 1.5f);
            _playerNumbers.TryGetValue(playerId, out int squadNumber);
            string markerText = MarkerLabelMode == PlayerMarkerLabelMode.Position
                ? _playerRoles[playerId]
                : squadNumber > 0 ? squadNumber.ToString() : "?";
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(point.X - playerRadius, point.Y + fontSize * 0.34f),
                markerText,
                HorizontalAlignment.Center,
                playerRadius * 2f,
                fontSize,
                Colors.White);
        }
    }

    private void DrawBall(Rect2 field)
    {
        if (!_isBallVisible)
        {
            return;
        }

        Vector2 ballPoint = ToFieldPoint(BallPosition, field);
        if (_ballActionActive)
            DrawLine(ballPoint, ToFieldPoint(_ballActionTo, field), new Color(1, 1, 1, 0.13f), 1);
        float liftPixels = _ballVisualHeight * field.Size.Y * 0.20f;
        float ballRadius = 4.5f + Mathf.Min(liftPixels * 0.10f, 1.4f);
        DrawCircle(ballPoint + new Vector2(1.5f + liftPixels, 2f + liftPixels), 5, new Color(0, 0, 0, 0.38f));
        DrawCircle(ballPoint, ballRadius, BallColor);
        DrawArc(ballPoint, ballRadius, 0, Mathf.Tau, 20, new Color("27313d"), 1);
    }

    private void DrawEllipticalArc(Vector2 center, Vector2 radius, float start, float end, Color color)
    {
        var points = new Vector2[25];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Lerp(start, end, i / 24f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
        }
        DrawPolyline(points, color, 2, true);
    }

    private static Vector2 ToFieldPoint(Vector2 normalized, Rect2 field) =>
        field.Position + new Vector2(normalized.X * field.Size.X, normalized.Y * field.Size.Y);

    public static Rect2 CalculateFieldRect(Vector2 controlSize)
    {
        const float horizontalMargin = 18f;
        const float verticalMargin = 10f;
        Vector2 availableSize = new(
            Mathf.Max(controlSize.X - horizontalMargin * 2f, 1f),
            Mathf.Max(controlSize.Y - verticalMargin * 2f, 1f));
        float availableAspect = availableSize.X / availableSize.Y;
        Vector2 fieldSize = availableAspect > FootballPitchDimensions.AspectRatio
            ? new Vector2(availableSize.Y * FootballPitchDimensions.AspectRatio, availableSize.Y)
            : new Vector2(availableSize.X, availableSize.X / FootballPitchDimensions.AspectRatio);
        Vector2 fieldPosition = (controlSize - fieldSize) * 0.5f;
        return new Rect2(fieldPosition, fieldSize);
    }
}
