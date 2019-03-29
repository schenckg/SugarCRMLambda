# SugarCRMLambda
This is an Amazon AWS Lambda Function that supports lookup of a Contact within the Sugar CRM and returns the name of the contact if found.  It is intended to be used specifically by Amazon's Cloud Phone System Connect call flows using an "Invoke AWS Lambda function" step.

The Lambda function should be configured with three environement variables in order to be able to connect to Sugar CRM.  
Specifically in the aws-lambda-tools-defaults.json file you'll want:

"environment-variables" : "\"SugarURL\"=\"https://<SUGAR URL HERE>/\";\"SugarUser\"=\"<SUGAR USER NAME HERE>\";\"SugarPassword\"=\"<SUGAR PASSWORD HERE>\""

Because the function is intended to be called from Amazon Connect it must be accept the standard JSON request data passed by Amazon Connect call flow when a Lambda function is invoked.  

This Amazon doc page describes the steps you'll need to take to use this code from an Amazon Connect Call Flow:

https://docs.aws.amazon.com/connect/latest/adminguide/connect-lambda-functions.html

The function expects input in JSON format as shown below.  When testing, the only field which must be set is the customer's phone number which is used to search in the Sugar CRM database, i.e., Details.ContactData.CustomerEndpoint.Address (in the example below we would lookup phone number "+1234567890"):
{
    "Details": {
        "ContactData": {
            "CustomerEndpoint": {
                "Address": "+1234567890"
            }
        }
    }
}

The function returns Connect compatible JSON formatted results.  Specifically if a contact is found matching the phone number then it's name is returned as shown here:
{
  "Contact": "\"Testy User\""
}

In the Amazon Connect Call Flow script the results would be accessed as the External Attribute "Contact".
