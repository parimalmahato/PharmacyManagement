# Application help assistant — phase 1

> This is the historical phase-1 guide. For the current implementation, including Ollama generation, Qdrant semantic RAG, invoice extraction, inventory questions, sales insights, reorder planning and sentiment analysis, use [AI_FEATURES.md](AI_FEATURES.md). Its configuration and scope supersede the phase-1 defaults below.

## Staff workflow

1. Sign in as Admin, Pharmacist or Cashier.
2. Open **Application Help** in the sidebar.
3. Choose an example or enter a question about an application workflow.
4. Read the answer and expand the retrieved **Guide sources**.
5. Follow the steps in the relevant application module. All existing role permissions apply.

The default mode retrieves reviewed user-guide sections locally. It is keyword-based document search, **not a language model or vector database**. The UI explicitly labels this mode. No API key, external service or database migration is required. `USER_GUIDE.md` is embedded at build/publish time, so rebuild and redeploy after editing it.

When configured, Langflow generates an answer using those retrieved sections. The sources are supplied by the backend, not invented links from model output. These are retrieved context sources, not proof that every generated claim is correct. Review guidance against them. Text is rendered as plain text, including guide Markdown, without executing HTML or model links.

## Enable generated answers through Langflow

This is optional and requires a separately deployed Langflow instance and an available language model. It does not install either service or guarantee free model usage.

1. In your Langflow deployment, create a **Chat Input → language model → Chat Output** flow. Connect the Chat Input text to the model input and the model's response to Chat Output. Use a stateless flow without database, filesystem, web or action tools and without conversation-memory retrieval.
2. Select and configure your model in Langflow. Configure model credentials there, never in React. A local model can be used if supported by your deployment and hardware.
3. Run the flow in Langflow and verify that it answers only from the supplied context, refuses unsupported/medical questions and preserves the documented role requirements.
4. Obtain the flow UUID and a Langflow API key from that deployment. Use HTTPS except for local loopback development. Keep Langflow private to the backend.
5. Set the following server environment variables (or equivalent deployment secret settings), then restart the API:

```text
HelpAssistant__EnableLangflow=true
HelpAssistant__BaseUrl=http://localhost:7860/
HelpAssistant__FlowId=<your-flow-UUID>
HelpAssistant__ApiKey=<your-Langflow-key>
```

6. Ask a known question in **Application Help**. Successful generation displays **AI-generated guidance**. Disabled, misconfigured, timed-out or invalid provider responses fall back to the user-guide sources.
7. To disable external generation, set `HelpAssistant__EnableLangflow=false` and restart.

The adapter uses Langflow's non-streaming `/api/v1/run/{flow_id}` API with `input_value`, chat input/output, a unique session ID and server-only `x-api-key`. It expects `outputs[].outputs[].results.message.text`. Verify compatibility with your deployed Langflow version using its generated API example. Reference: [Langflow flow trigger API](https://docs.langflow.org/api-flows-run).

## Data and boundaries

- The authenticated endpoint is `POST /api/help-assistant/ask` with JSON `{"question":"How do I record a split payment?"}`.
- Input is limited to 1,000 characters and 8 KB per request. Requests are rate-limited to 10 per account per minute.
- It has no stock, patient/customer, invoice, payment or refund database connection. It neither queries nor changes business records. Shared workflow documentation is the same for each branch; changing accounts/branches clears the screen through the existing workspace remount.
- When generation is enabled, the question and up to three guide sections are sent to the configured Langflow service and potentially its model provider. Do not enter personal information or payment credentials. Configure provider retention and deployment access before customer use. The application does not persist chat history, but the provider may log requests.
- A new provider session ID is used per question. Follow-up questions are independent; restate the task rather than relying on conversation memory.
- The complete shared guide includes procedures for every role. Explaining an Admin procedure does not grant an employee permission to execute it.
- Generated answers are advisory and may be inaccurate. Prompt instructions are not an authorization mechanism. The flow must have no operational tools, and financial and inventory actions stay in the existing validated application endpoints.
- No question text, provider response body or API key is written by this adapter to application logs. Provider failures log only their status/type.
- There is no automatic action execution, medical advice, invoice scanning or live inventory assistant in this phase.

## Vector database and sentiment analysis

These are not enabled in phase 1. `IHelpKnowledge` is the replacement boundary for a later embedding/vector-search implementation. Only approved help documents should be indexed; live financial records must remain behind authorized SQL queries. Langflow is connected for generation, not represented as an installed vector database.

Sentiment analysis should be a separate module once customer feedback collection and consent/retention requirements exist. There is currently no feedback corpus to analyze.

## Validation

Automated tests cover packaged-document retrieval, empty/oversized questions, no-match handling, disabled generation, provider request shape/private key/session separation, malformed/error response fallback and source preservation. A real Langflow/model call needs configured services and credentials and is not covered by those stubbed tests. Before enabling it for staff, evaluate representative questions, unsupported questions, prompt injection attempts and role-specific instructions against the guide.
