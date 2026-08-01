using System.Collections.Generic;

public interface IFootballActionCandidateGenerator
{
    void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates);
}
