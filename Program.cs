using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSService<IAmazonS3>();

var app = builder.Build();

app.MapPut("/s3files/{bucketName}/{objectKey}", async (HttpContext context, string bucketName, string objectKey, IAmazonS3 amazonS3) =>
{
    bool bucketExists = await amazonS3.DoesS3BucketExistAsync(bucketName);
    if (!bucketExists)
    {
        return Results.NotFound($"Bucket {bucketName} does not exist.");
    }
    // Get the uploaded file from the request
    // Avoid user or programmer mistakes like Http-415
    var form = await context.Request.ReadFormAsync();
    var image = form.Files.GetFile("image");

    if (image == null)
    {
        return Results.BadRequest("File not found in request.");
    }

    // Delete the old object with the same object key
    await amazonS3.DeleteObjectAsync(bucketName, objectKey);

    // Upload the new image as a new object with the same object key
    PutObjectRequest request = new()
    {
        BucketName = bucketName,
        Key = objectKey,
        InputStream = image.OpenReadStream()
    };

    request.Metadata.Add("Content-Type", image.ContentType);
    await amazonS3.PutObjectAsync(request);

    return Results.Ok($"File {objectKey} updated in S3 successfully!");
});

app.MapPost("/upload", async (HttpContext context, string bucketName, string? prefix, IAmazonS3 amazonS3) =>
{
    bool bucketExists = await amazonS3.DoesS3BucketExistAsync(bucketName);
    if (!bucketExists)
    {
        return Results.NotFound($"Bucket {bucketName} does not exist.");
    }
    var form = await context.Request.ReadFormAsync();
    var image = form.Files.GetFile("image");

    if (image == null)
    {
        return Results.BadRequest("File not found in request.");
    }

    try
    {
        using var stream = image.OpenReadStream();
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = string.IsNullOrEmpty(prefix) ? image.FileName : $"{prefix.TrimEnd('/')}/{image.FileName}",
            InputStream = stream,
            ContentType = image.ContentType            
        };
        await amazonS3.PutObjectAsync(putRequest);
        return Results.Ok($"File {putRequest.Key} uploaded to {bucketName} successfully!");
    }
    catch (AmazonS3Exception ex)
    {
        return Results.BadRequest($"Error uploading image: {ex.Message}");
    }

});

app.MapGet("/download/{imagePath}", async (string imagePath, IAmazonS3 amazonS3) =>
{
    // Replace with your own cdn
    string cdnUrl = "https://dw98tylghuyai.cloudfront.net/"; 
    string[] imageParts = imagePath.Split('/');
    string objectKey = string.Join('/', imageParts);

    var imageUrl = cdnUrl + objectKey;
    
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(imageUrl);
    if (!response.IsSuccessStatusCode)
    {
        return Results.NotFound();
    }
    var stream = await response.Content.ReadAsStreamAsync();

    return Results.File(stream, response.Content.Headers.ContentType?.MediaType);
});

app.Run();
