using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSService<IAmazonS3>();

var app = builder.Build();

app.MapGet("/files", async (IAmazonS3 amazonS3, string bucketName, string? prefix) =>
{
    bool bucketExists = await amazonS3.DoesS3BucketExistAsync(bucketName);
    if (!bucketExists)
    {
        return Results.NotFound($"Bucket {bucketName} does not exist");
    }

    ListObjectsV2Request request = new()
    {
        BucketName = bucketName,
        Prefix = prefix
    };

    ListObjectsV2Response response = await amazonS3.ListObjectsV2Async(request);

    List<S3ObjectsDTO> filesDatas = response.S3Objects.Select(obj =>
    {
        GetPreSignedUrlRequest urlRequest = new()
        {
            BucketName = bucketName,
            Key = obj.Key,
            Expires = DateTime.UtcNow.AddMinutes(1)
        };
        return new S3ObjectsDTO
        {
            Name = obj.Key,
            FileUrl = amazonS3.GetPreSignedURL(urlRequest)
        };
    }).ToList();

    return Results.Ok(filesDatas);
});

//@IFormFile object passed as a parameter to the handler delegate can handle any type of file, including images.
app.MapPut("/s3files/{bucketName}/{objectKey}", async (HttpContext context, string bucketName, string objectKey, IAmazonS3 amazonS3) =>
{
    bool bucketExists = await amazonS3.DoesS3BucketExistAsync(bucketName);
    if (!bucketExists)
    {
        return Results.NotFound($"Bucket {bucketName} does not exist.");
    }

    // Get the uploaded file from the request
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

app.Run();
