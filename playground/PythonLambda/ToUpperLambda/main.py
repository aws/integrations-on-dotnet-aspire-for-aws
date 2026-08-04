"""
ToUpper Lambda – Python example for Aspire.Hosting.AWS Python Lambda support.

Handler: main.handler

Receives an API Gateway HTTP API (v2) event and returns the body uppercased.
Also reads/writes a per-word invocation counter to a DynamoDB Local table so
the end-to-end DynamoDB flow can be exercised locally.

Environment variables injected by Aspire:
  AWS_ENDPOINT_URL_DYNAMODB   – DynamoDB Local endpoint (when .WithReference(dynamoDBLocal) is used)
  AWS_REGION / AWS_DEFAULT_REGION
"""

import json
import logging
import os

import boto3
from botocore.config import Config

logger = logging.getLogger()
logger.setLevel(logging.INFO)

_TABLE_NAME = os.environ.get("COUNTER_TABLE_NAME", "InvocationCounters")


def _get_dynamodb():
    """Return a DynamoDB resource, pointing at Local if the env var is set."""
    endpoint_url = os.environ.get("AWS_ENDPOINT_URL_DYNAMODB")
    kwargs = {}
    if endpoint_url:
        kwargs["endpoint_url"] = endpoint_url
        kwargs["config"] = Config(retries={"max_attempts": 1})
    return boto3.resource("dynamodb", **kwargs)


def _increment_counter(word: str) -> int:
    """Atomically increment and return the invocation counter for *word*."""
    try:
        table = _get_dynamodb().Table(_TABLE_NAME)
        response = table.update_item(
            Key={"Word": word},
            UpdateExpression="ADD InvocationCount :inc",
            ExpressionAttributeValues={":inc": 1},
            ReturnValues="UPDATED_NEW",
        )
        return int(response["Attributes"]["InvocationCount"])
    except Exception as exc:  # noqa: BLE001
        logger.warning("DynamoDB counter update failed (non-fatal): %s", exc)
        return -1


def handler(event, context):
    """Lambda entry-point."""
    logger.info("Received event: %s", json.dumps(event))

    # Support both direct invocation (plain string body) and API-GW HTTP events
    raw_body = event.get("body") or event if isinstance(event, str) else ""
    if isinstance(event, dict):
        raw_body = event.get("body", "")

    text = (raw_body or "").strip()
    upper = text.upper()

    count = _increment_counter(upper) if upper else 0

    return {
        "statusCode": 200,
        "headers": {"Content-Type": "application/json"},
        "body": json.dumps({"result": upper, "invocationCount": count}),
    }
