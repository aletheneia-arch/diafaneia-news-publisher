using KaragoPublisher.Windows;

var passed = 0;
void Check(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected '{expected}', got '{actual}'");
}

Check("JPEG bytes override misleading WebP filename/MIME", () =>
{
    var image = new ConnectorImage
    {
        Name = "facebook-feature.webp",
        MimeType = "image/webp",
        DataBase64 = Convert.ToBase64String(new byte[] { 0xff, 0xd8, 0xff, 0xe0 })
    };
    Equal("image/jpeg", image.MimeType);
    Equal("facebook-feature.jpg", image.Name);
});

Check("PNG bytes override misleading JPG filename/MIME", () =>
{
    var image = new ConnectorImage
    {
        Name = "facebook-feature.jpg",
        MimeType = "image/jpeg",
        DataBase64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })
    };
    Equal("image/png", image.MimeType);
    Equal("facebook-feature.png", image.Name);
});

Check("unknown/corrupt bytes are rejected", () =>
{
    try
    {
        _ = new ConnectorImage
        {
            Name = "bad.jpg",
            MimeType = "image/jpeg",
            DataBase64 = Convert.ToBase64String(new byte[] { 0x00, 0x01, 0x02 })
        };
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new Exception("corrupt image was not rejected");
});

Console.WriteLine($"Image acceptance: {passed}/3 PASS");
