using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSService<IAmazonS3>();

var app = builder.Build();

//@IFormFile object passed as a parameter to the handler delegate can handle any type of file, including images.
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
    var file = form.Files.GetFile("file");

    if (file == null)
    {
        return Results.BadRequest("File not found in request.");
    }

    // Delete the old object with the same object key
    await amazonS3.DeleteObjectAsync(bucketName, objectKey);

    // Upload the new file as a new object with the same object key
    PutObjectRequest request = new()
    {
        BucketName = bucketName,
        Key = objectKey,
        InputStream = file.OpenReadStream()
    };

    request.Metadata.Add("Content-Type", file.ContentType);
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
    var file = form.Files.GetFile("file");

    if (file == null)
    {
        return Results.BadRequest("File not found in request.");
    }

    try
    {
        using var stream = file.OpenReadStream();
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix.TrimEnd('/')}/{file.FileName}",
            InputStream = stream,
            ContentType = file.ContentType            
        };
        await amazonS3.PutObjectAsync(putRequest);
        return Results.Ok($"File {putRequest.Key} uploaded to {bucketName} successfully!");
    }
    catch (AmazonS3Exception ex)
    {
        return Results.BadRequest($"Error uploading file: {ex.Message}");
    }

});

app.MapGet("/download/{filePath}", async (string filePath, IAmazonS3 amazonS3) =>
{
    // Replace with your won cdn
    string cdnUrl = "https://dw98tylghuyai.cloudfront.net/"; 
    string[] fileParts = filePath.Split('/');
    string objectKey = string.Join('/', fileParts);

    var fileUrl = cdnUrl + objectKey;
    
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(fileUrl);
    if (!response.IsSuccessStatusCode)
    {
        return Results.NotFound();
    }
    var stream = await response.Content.ReadAsStreamAsync();

    return Results.File(stream, response.Content.Headers.ContentType?.MediaType);
});

app.Run();
