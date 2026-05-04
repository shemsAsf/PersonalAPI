namespace PersonalApi.Projects.EkaterinaPotapovaDesign;

public record ProjectSummary(
    int Id,
    string Title,
    string Subtitle,
    string Cover,
    List<string> Tools,
    bool Visible
);

public record ProjectIndex(List<ProjectSummary> Projects);