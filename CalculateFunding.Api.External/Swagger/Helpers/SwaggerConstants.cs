﻿namespace CalculateFunding.Api.External.Swagger.Helpers
{
    public class SwaggerConstants
    {
        public const string StoreExportLocation = "CalculateFunding.Api.External.StoreExport";

        public const string TopIntroductionText =
@"
# Calculate Funding Service 
# Service Context
### Purpose of the Service
The Calculate Funding Service manages the specification, calculation, testing and publishing of provider allocations. It's main responsibilities include:

* Specifying funding policies, linked to funding streams and allocation lines
* Defining calculations that implement specified policies
* Defining and importing datasets that provider source data required by the calculations
* Supporting the creation and execution of test scenarios that validate the calculation results
* Supporting the publishing of correct allocations through viewing the results of tests and calculations
* Providing an API that allows external systems to obtain details of published allocations

    
## Governance
TBC

## Usage Scope
This Service is designed for internal Agency use only.

## Availability
TBC

## Performance and Scalability
TBC

## Pre-Requisites
* Client must have network access to the calculate funding api
* Client must have the necessary credentials to access the API
* Client must trust the server certificate used for SSL connections.

## Post-Requisites
* None

## Media Types
The following table lists the media types used by the service:

| Media Type    | Description |
| ------------- |-------------| 
| application/vnd.sfa.allocation.{VERSION}+xml | An allocation in XML format |
| application/vnd.sfa.allocation.{VERSION}+json | An allocation in JSON format  |
| application/vnd.sfa.allocation.{VERSION}+atom+xml | An atom feed representing a stream of allocations in XML format. Each content item in the feed will be vnd.sfa.allocation.{VERSION} |
| application/vnd.sfa.allocation.{VERSION}+atom+json | An atom feed representing a stream of allocations in JSON format.* Each content item in the feed will be vnd.sfa.allocation.{VERSION}  |

* This not a part of the ATOM standard but is a convenience feature for native JSON clients.
The media Type above conform to the Accept Header specification. In simple terms that states that the media Type is vendor specific, is a given representation (sfa.allocation and version) and delivered in a particular wire format (JSON or XML).

## Request Headers
The following HTTP headers are supported by the service.

| Header | Optionality    | Description |
| ------------- |-------------|---------------| 
| Accept     | Required | The calculate funding service uses the Media Type provided in the Accept header to determine what representation of a particular resources to serve. In particular this includes the version of the resource and the wire format. |

## Response Header
There are no custom headers returned by the service. However each call will return header to aid the client with caching responses.

## Page of Results
The calculate funding API provides resources that represent notification streams. These will be provided as an ATOM feed. The ATOM specification makes provision for paging results in two ways. Both methods provides a means for client to navigate the stream without prior knowledge of the necessary URIs. This follows a Hypermedia As The Engine Of Application State (HATEAOS) pattern. The selected paging method will depend on the specific resource. Clients are expected to be tolerant to changes to the paging method within the confines of the ATOM specification 

## Generic Error and Exception Behaviour
All operations will return one of the following messages in the event a generic exception is encountered.

| Error | Description    | Should Retry |
| ------------- |-------------|---------------| 
| 401 Unauthorized | The consumer is not authorized to use this service. | No |
| 404 Not Found  | The resource requested cannot be found. Check the structure of the URI  | No |
| 410 Gone | The requested resource is no longer available at the server and no forwarding address is known. This will be used when a previously supported resource URI has been depreciated.  | No
| 415 Unsupported Media Type |  The media Type provide in the Accept header is not supported by the server. This error will also be produced if the client requests a version of a media Type not supported by the service. | No
| 500 Internal Server Error | The service encountered an unexpected error. The service may have logged an error, and if possible the body will contain a text/plain guid that can be used to search the logs. | Unknown, so limited retries may be attempted
| 503 Service Unavailable |  The service is too busy | Yes
";
    }
}