namespace HNReader.Server;

internal record DigestDto(
    IEnumerable<Categories> Categories);

internal record Categories(
    string Name, 
    string Summary,
    IEnumerable<DigestItemDto> Items);

internal record DigestItemDto(
    string Title,
    string Url,
    string Summary,
    string Author
);