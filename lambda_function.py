import boto3
import json
import uuid
import os
from botocore.config import Config

# Configure S3 client to force SigV4 and virtual-host addressing
# This prevents "TemporaryRedirect" and "SignatureDoesNotMatch" errors
s3_config = Config(
    region_name=os.environ.get('AWS_REGION', 'us-east-2'),
    signature_version='s3v4',
    s3={'addressing_style': 'virtual'}
)

s3_client = boto3.client(
    's3',
    config=s3_config
)

BUCKET_NAME = os.environ.get('INTAKE_BUCKET', 'swingforge-intake')


def lambda_handler(event, context):
    """
    Generate presigned URLs for uploading swing videos to S3.
    """
    try:
        # Parse request body
        body = {}
        if event.get('body'):
            body = json.loads(event['body'])
        
        user_id = body.get('user_id')
        if not user_id:
            return error_response(400, 'user_id is required')
        
        session_id = body.get('session_id') or str(uuid.uuid4())
        
        # Optional: use client filenames so S3 keys keep original names (e.g. 20260131-192536.mp4)
        face_on_filename = _sanitize_filename(body.get('face_on_filename'), 'face-on.mp4')
        down_the_line_filename = _sanitize_filename(body.get('down_the_line_filename'), 'down-the-line.mp4')
        
        # S3 layout: CLIENTID/HeadOn/<file>, CLIENTID/DTL/<file>, CLIENTID/metadata/<session_id>.json
        expiration = 3600  # 1 hour
        urls = {
            'session_id': session_id,
            'face_on_url': generate_presigned_url(f"{user_id}/HeadOn/{face_on_filename}", expiration, 'application/octet-stream'),
            'down_the_line_url': generate_presigned_url(f"{user_id}/DTL/{down_the_line_filename}", expiration, 'application/octet-stream'),
            'metadata_url': generate_presigned_url(f"{user_id}/metadata/{session_id}.json", expiration, 'application/json'),
            'expires_in': expiration
        }
        
        return {
            'statusCode': 200,
            'headers': {
                'Content-Type': 'application/json',
                'Access-Control-Allow-Origin': '*',
                'Access-Control-Allow-Headers': 'Content-Type',
                'Access-Control-Allow-Methods': 'POST, OPTIONS'
            },
            'body': json.dumps(urls)
        }
        
    except json.JSONDecodeError:
        return error_response(400, 'Invalid JSON in request body')
    except Exception as e:
        print(f"Error generating presigned URLs: {str(e)}")
        return error_response(500, 'Internal server error')


def _sanitize_filename(value: str, default: str) -> str:
    """Use only the base name; reject path traversal. Fall back to default if invalid."""
    if not value or not isinstance(value, str):
        return default
    name = os.path.basename(value.strip())
    if not name or '..' in name or '/' in name or '\\' in name:
        return default
    return name


def generate_presigned_url(key: str, expiration: int, content_type: str = 'application/octet-stream') -> str:
    """Generate a presigned URL for PUT operation. content_type must match the client's Content-Type header."""
    return s3_client.generate_presigned_url(
        'put_object',
        Params={
            'Bucket': BUCKET_NAME,
            'Key': key,
            'ContentType': content_type
        },
        ExpiresIn=expiration
    )


def error_response(status_code: int, message: str) -> dict:
    """Return an error response."""
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*'
        },
        'body': json.dumps({'error': message})
    }