# Lambda Deployment Guide

## Function: swingforge-presigned-url-generator

### 1. Create the Lambda Function

**Via AWS Console:**

1. Go to AWS Lambda → Create function
2. Settings:
   - **Name:** `swingforge-presigned-url-generator`
   - **Runtime:** Python 3.12
   - **Architecture:** x86_64
   - **Handler:** `lambda_function.lambda_handler`

3. Configuration:
   - **Memory:** 128 MB
   - **Timeout:** 10 seconds

4. Environment Variables:
   - `INTAKE_BUCKET`: `swingforge-intake`
   - `AWS_REGION`: `us-east-2`

5. Upload code:
   - Zip `lambda_function.py` and upload (see **Lambda code with ContentType** below to avoid S3 403 Forbidden)

**Lambda code with ContentType (required for S3 presigned PUT):**

Use the code in this repo: **`aws-cloud-backend/lambda_function.py`**. Do not use older snippets that used `CaptureA`/`CaptureB` or `{user_id}/{session_id}/` layout.

The client sends `Content-Type: application/octet-stream` for video files and `application/json` for metadata. The presigned URL must be generated with the same `ContentType` in `Params`, or S3 returns 403 Forbidden.

**S3 layout (current):** Flat under `{user_id}/`: `{user_id}/{filename}.mp4`, `{user_id}/{filename}.kva`, `{user_id}/{session_id}.json`, `{user_id}/{session_id}.pose3d.json`. Filenames are distinct (e.g. headon-*, dtl-*), so no subfolders.

**Via AWS CLI:**

```bash
# From repo root: zip the function (handler stays lambda_function.lambda_handler)
cd aws-cloud-backend
zip function.zip lambda_function.py

# Create the function
aws lambda create-function \
  --function-name swingforge-presigned-url-generator \
  --runtime python3.12 \
  --handler lambda_function.lambda_handler \
  --zip-file fileb://function.zip \
  --role arn:aws:iam::YOUR_ACCOUNT_ID:role/swingforge-lambda-role \
  --timeout 10 \
  --memory-size 128 \
  --environment "Variables={INTAKE_BUCKET=swingforge-intake}"
```

### 2. Create IAM Role

Create a role with this policy:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "logs:CreateLogGroup",
                "logs:CreateLogStream",
                "logs:PutLogEvents"
            ],
            "Resource": "arn:aws:logs:*:*:*"
        },
        {
            "Effect": "Allow",
            "Action": [
                "s3:PutObject"
            ],
            "Resource": "arn:aws:s3:::swingforge-intake/*"
        }
    ]
}
```

### 3. Create API Gateway

1. Go to API Gateway → Create API
2. Choose **HTTP API** (not REST API - cheaper)
3. Add integration:
   - Integration type: Lambda
   - Lambda function: `swingforge-presigned-url-generator`
4. Configure route:
   - Method: POST
   - Path: `/request-upload`
5. Configure CORS:
   - Allow origins: `*`
   - Allow methods: `POST, OPTIONS`
   - Allow headers: `Content-Type`
6. Deploy and note the endpoint URL

### 4. Test the Endpoint

```bash
curl -X POST https://YOUR_API_ID.execute-api.us-east-2.amazonaws.com/request-upload \
  -H "Content-Type: application/json" \
  -d '{"user_id": "test-user-123"}'
```

Expected response:

```json
{
  "session_id": "generated-uuid",
  "face_on_url": "https://swingforge-intake.s3.amazonaws.com/...",
  "down_the_line_url": "https://swingforge-intake.s3.amazonaws.com/...",
  "metadata_url": "https://swingforge-intake.s3.amazonaws.com/...",
  "expires_in": 3600
}
```

### 5. Save Your Endpoint URL

After deployment, save the API Gateway endpoint URL. You'll need to enter this in SwingForge.App settings.

Example: `https://abc123xyz.execute-api.us-east-2.amazonaws.com`

---

## End-to-End Testing

### Test 1: Verify Lambda Function

```bash
# Test the Lambda directly
curl -X POST https://YOUR_API_ID.execute-api.us-east-2.amazonaws.com/request-upload \
  -H "Content-Type: application/json" \
  -d '{"user_id": "test-user-123"}'
```

Expected: JSON response with presigned URLs.

### Test 2: Verify Presigned URL Upload

```bash
# Use one of the presigned URLs to upload a test file
echo '{"test": true}' > test.json
curl -X PUT "PRESIGNED_METADATA_URL_FROM_TEST_1" \
  -H "Content-Type: application/json" \
  --data-binary @test.json
```

Expected: HTTP 200 OK. Check S3 console for the uploaded file.

### Test 3: SwingForge.App Integration

1. Launch SwingForge.App
2. Go to Settings
3. Enable "Cloud Backup"
4. Enter your API Gateway endpoint URL
5. Click "Save"
6. Capture a video pair (or use existing videos in monitored folders)
7. Click "Sync Now" or wait for the batch timer
8. Check S3 console for uploaded files:
   - `swingforge-intake/{user_id}/{filename}.mp4` (e.g. headon-*.mp4, dtl-*.mp4)
   - `swingforge-intake/{user_id}/{filename}.kva`
   - `swingforge-intake/{user_id}/{session_id}.json`

### Test 4: Verify Metadata

Download and inspect `metadata.json` from S3:

```bash
aws s3 cp s3://swingforge-intake/{user_id}/{session_id}.json - | jq .
```

Expected fields:
- `user_id`: Your device's UUID
- `session_id`: Unique session identifier
- `upload_timestamp`: When the upload occurred
- `capture_timestamp`: When the video was captured
- `client_version`: SwingForge version
- `camera_settings`: fps, resolution

### Test 5: Failure Recovery

1. Disable network connection
2. Capture a video
3. Check that session appears as "pending" in Settings
4. Re-enable network
5. Click "Sync Now" or wait for batch timer
6. Verify upload succeeds

### Success Criteria

- [ ] Videos upload from SwingForge to S3 staging
- [ ] Metadata included with all required fields
- [ ] Can see files in S3 console with correct paths
- [ ] Presigned URLs working correctly
- [ ] Client handles upload failures gracefully
- [ ] Retry logic works on network failure

---

## Running SwingForge.exe: "Side-by-side configuration is incorrect"

If SwingForge fails to start with that error, the app’s native FFmpeg DLL (built with Visual Studio 2022) needs the **Microsoft Visual C++ 2015–2022 Redistributable (x64)**.

1. **Install the runtime:**  
   Download and run: **https://aka.ms/vs/17/release/vc_redist.x64.exe**

2. **Confirm the failing dependency (optional):**  
   In an elevated Command Prompt:  
   `sxstrace trace -logfile:sxs.etl`  
   Run SwingForge.exe, then stop the trace.  
   `sxstrace parse -logfile:sxs.etl -outfile:sxs.txt`  
   Open `sxs.txt` to see which assembly/DLL could not be resolved.
