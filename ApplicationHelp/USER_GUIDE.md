# Pharmacy Management: step-by-step user guide

For pharmacy owners, administrators, pharmacists and cashiers. This guide describes the current application menus and workflows. Your account role determines which pages and actions are available.

## Contents

1. [Sign in and navigate](#1-sign-in-and-navigate)
2. [Set up the pharmacy and branches](#2-set-up-the-pharmacy-and-branches)
3. [Create employee accounts and manage passwords](#3-create-employee-accounts-and-manage-passwords)
4. [Create the medicine catalog](#4-create-the-medicine-catalog)
5. [Enter opening stock](#5-enter-opening-stock)
6. [Purchase and receive medicines](#6-purchase-and-receive-medicines)
7. [Register customers](#7-register-customers)
8. [Create a sale and print the invoice](#8-create-a-sale-and-print-the-invoice)
9. [Collect and record payments](#9-collect-and-record-payments)
10. [Process returns, inspection and refunds](#10-process-returns-inspection-and-refunds)
11. [Cancel an incorrectly created sale](#11-cancel-an-incorrectly-created-sale)
12. [Monitor stock, expiry and notifications](#12-monitor-stock-expiry-and-notifications)
13. [Use the dashboard and export reports](#13-use-the-dashboard-and-export-reports)
14. [Prepare the monthly GST worksheet](#14-prepare-the-monthly-gst-worksheet)
15. [Daily operating routine](#15-daily-operating-routine)
16. [Troubleshooting](#16-troubleshooting)
17. [Use AI assistance](#17-use-ai-assistance)

## Who can do what?

| Task | Admin | Pharmacist | Cashier |
| --- | --- | --- | --- |
| Manage branches, employees and pharmacy settings | Yes | No | No |
| View medicines and inventory | Yes | Yes | Yes |
| Manage medicines and opening stock | Yes | Yes | No |
| Create purchases and receive stock | Yes | Yes | No |
| View customers and sales invoices | Yes | Yes | Yes |
| Register or edit customers | Yes | No | Yes |
| Create sales and record payments | Yes | No | Yes |
| Record returns, inspection approval and refunds | Yes | No | Yes |
| Cancel eligible sales | Yes | Yes | No |
| Dashboard, reports, expiry and notifications | Yes | Yes | No |
| GST preparation and audit logs | Yes | No | No |
| Change own password | Yes | Yes | Yes |

Inspection approval currently belongs to the return workflow available to Admins and Cashiers. It is an explicit approval by the authorized operator; the application does not require a separate second approver.

## 1. Sign in and navigate

1. Open the application address supplied by your administrator.
2. Enter your account email and password, then sign in.
3. If asked to change your initial password, enter the current temporary password, a new password and its confirmation. Complete the change, then sign in again.
4. Check the **Active branch** displayed at the top before entering transactions.
5. Use the left sidebar to open a module. On smaller screens, open the navigation menu first.
6. Use **Logout** when you finish, especially on a shared billing computer.

Cashiers land on **Sales / Billing**. Admins and Pharmacists land on **Dashboard**. Browser refresh may require you to sign in again.

Admins can select the active branch. Other employees work in their assigned branch. Switching branches clears unsaved forms, so finish or abandon your current form first.

**Branch rule:** medicine and supplier catalogs are shared. Stock, purchases, customers, sales, payments, returns and pharmacy settings belong to the selected branch. Stock in another branch is not available for the current branch's sale.

## 2. Set up the pharmacy and branches

**Performed by: Admin. Complete this before regular billing.**

1. Obtain the initial Admin account from the person who installed the application and complete the required password change.
2. Open **Branches**. Check the existing Main branch and create additional branches with their branch codes and names.
3. Select the branch to configure using **Active branch**.
4. Open **Pharmacy Settings** and enter the pharmacy name, contact details, address and applicable GSTIN/GST configuration.
5. Set the invoice numbering preferences before issuing invoices. The application manages the invoice sequence; do not attempt to reuse issued numbers.
6. Configure low-stock thresholds, expiry alert preferences and receipt preferences, including any footer and customer/payment details you want printed.
7. Save the settings and check that they reload correctly.
8. Repeat for each branch. A new branch needs its own configuration and stock.
9. If collecting payments through UPI QR, configure that branch's merchant UPI ID and payee name and enable UPI collection. Use the bank-provided merchant category code only if supplied.

Next, create the medicine catalog, enter genuine opening stock or receive purchases, and create employee accounts. See [pharmacy settings details](backend/Documents/PHARMACY_SETTINGS.md).

## 3. Create employee accounts and manage passwords

### Onboard a Pharmacist or Cashier

1. As Admin, open **Employee Management**.
2. Enter the employee's full name, email and initial password.
3. Select **Pharmacist** or **Cashier** and the assigned branch.
4. Click **Create Employee**.
5. Give the employee their login details privately. Each employee should use their own account.
6. Ask the employee to sign in, replace the initial password and check their assigned branch.

Passwords must contain at least 12 characters, including uppercase and lowercase letters, a number and a special character. Admins can activate or deactivate employee accounts from this page.

### Change your own password

1. Select **Change password** in the sidebar account area.
2. Enter your current password, new password and confirmation.
3. Submit the change and sign in again when prompted.

### Forgotten employee password

1. The employee contacts the pharmacy Admin.
2. The Admin opens **Employee Management** and selects **Reset password** for that employee.
3. Enter and confirm a temporary password, then submit the reset.
4. Share the temporary password privately.
5. The employee signs in and must choose a new password before continuing.

A reset invalidates existing sessions. It does not automatically reactivate a disabled account. This employee reset screen does not reset Admin accounts, and there is no email/SMS self-service recovery. For a forgotten Admin password, contact the deployment administrator. See [account access details](backend/Documents/ACCOUNT_ACCESS.md).

## 4. Create the medicine catalog

**Performed by: Admin or Pharmacist.**

1. Open **Medicines** and select **+ Add medicine**.
2. Enter the medicine details requested by the form.
3. Check the HSN code and GST rate before saving, particularly before the first sale of that medicine.
4. Save the medicine and confirm that it appears in the catalog.
5. Use **Edit** to correct catalog details or **Deactivate** when the medicine should no longer be offered.

Adding a medicine does not add stock. Enter a batch through opening stock or purchase receiving. Catalog changes do not rewrite tax details already captured on issued invoices.

## 5. Enter opening stock

**Performed by: Admin or Pharmacist, for stock already held when starting the system.**

1. Physically count the medicine by batch and check its expiry and condition.
2. Open **Inventory** and open **Register opening stock**.
3. Select the medicine and enter the actual batch number and expiry date.
4. Enter purchase price per unit, selling price per unit and opening quantity. Use the same unit consistently for purchasing, stock and billing.
5. Select **Register batch**.
6. Confirm the medicine, batch and quantity in **Batch Inventory**.

Enter only eligible stock. Keep expired, damaged or otherwise non-saleable medicines physically separated. For subsequent supplier deliveries, use Purchasing. Do not enter the same delivery as both opening stock and a received purchase.

## 6. Purchase and receive medicines

**Performed by: Admin or Pharmacist.**

1. Open **Purchasing**.
2. If necessary, use **Add supplier** to register the supplier's name and contact details.
3. Under **Create purchase invoice**, select the supplier and enter the supplier invoice number and date.
4. Enter each medicine's batch, future expiry date, quantity, purchase price and selling price. Use **+ Add item** for additional lines.
5. Check the information against the supplier invoice, then select **Create draft purchase**.
6. Inspect and count the actual delivery. A draft has not changed inventory.
7. In **Purchase history**, select **Receive stock** for the correct draft and confirm.
8. Check that its status is received and the quantities appear in **Inventory** for the active branch.

Receiving changes stock. Avoid creating another draft for a delivery already recorded. If a received invoice contains an error, contact the Admin for investigation rather than compensating with an unrelated opening-stock entry.

## 7. Register customers

**Performed by: Admin or Cashier.**

1. Open **Customers**.
2. Under **Register Customer**, enter the customer's name and any available phone, email and address.
3. Select **Register Customer**.
4. Search for the customer to confirm the record. Use **Edit** when details change.
5. Select this customer during billing to associate the invoice with them.

Use **Walk-in Customer** for an unnamed sale. Customer records belong to the active branch. Pharmacists can view customer records but cannot maintain them.

## 8. Create a sale and print the invoice

**Performed by: Admin or Cashier.**

1. Open **Sales / Billing** and check the active branch.
2. In **New Sale**, select the customer or keep **Walk-in Customer**.
3. Select a medicine, enter **Quantity** and any authorized **Discount %**, then select **Add to Cart**.
4. Repeat for all medicines. Review quantities and discounts; remove incorrect lines before checkout.
5. Review the subtotal, discount, taxable value, GST and total. Selling prices are treated as GST-inclusive.
6. Select **Complete Sale** once.
7. Wait for the successful invoice. The application allocates eligible batches using earliest expiry first and reduces stock when the sale is committed. Final batch allocation can affect the total shown in the initial cart estimate.
8. Select **Print Invoice**, or **Open Full Invoice** to review and print the complete invoice.
9. Continue to the invoice's payment section to record the money received.

**Complete Sale creates the invoice; it does not establish that the customer paid.** Unsold cart items can be removed or cleared before checkout. An issued invoice must be handled through cancellation or returns rather than editing its original totals.

To find a past sale, use **Sales History**, then **Payments** or **View invoice**. Pharmacists can view sales and invoices but cannot create sales.

If checkout is interrupted and a pending checkout banner appears, use **Retry pending checkout**. Do not create a replacement invoice while the first checkout is unresolved.

## 9. Collect and record payments

**Performed by: Admin or Cashier.**

### Cash, card, partial and split payments

1. Open the invoice's **Payments** section.
2. Check the outstanding balance and existing payment history.
3. Collect the money using the agreed method.
4. Select **Cash**, **UPI** or **Card**, and enter the amount actually received.
5. Enter the real transaction reference for UPI or Card. Cash reference is optional.
6. For multiple methods, select **Add split payment** and enter each receipt separately. **Fill remaining balance** helps fill the unpaid amount.
7. Submit **Record payment** and check the updated balance and history.

A partial payment leaves an outstanding balance. Total receipts cannot exceed the amount due. Recording a Card receipt does not operate a card terminal; complete that transaction separately first.

### Collect through UPI QR

1. Confirm that the Admin enabled UPI collection for this branch.
2. Under **Collect with UPI**, enter the amount to collect and select **Show UPI QR**.
3. Ask the customer to scan and verify the payee and amount. **Open a UPI app on this device** is available where supported.
4. Check the actual credited transaction in the pharmacy's bank or merchant application.
5. In the receipt form, choose UPI and enter the received amount and actual transaction ID/UTR.
6. Tick the confirmation that you verified the credit and transaction ID, then record the payment.

The QR reference is not the bank UTR. Scanning, a screenshot or returning from a UPI app does not mark the invoice paid. This direct UPI workflow has no automatic bank confirmation. Merchant QR acceptance depends on the bank and payment app; test your configured account and devices before customer use. See [UPI collection details](backend/Documents/UPI_COLLECTION.md).

If a receipt submission is interrupted, inspect history and use **Retry pending receipts** when offered. Keep the same pending operation until its result is known.

## 10. Process returns, inspection and refunds

**Performed by: Admin or Cashier.**

### Record returned medicines

1. Find the original invoice and open **Returns and refunds**.
2. Check the return history and remaining quantity available to return.
3. Under **Return medicines**, choose the original sold item/batch and enter the returned quantity.
4. Inspect the physical medicine and select its disposition: saleable, damaged, expired, quarantine/uninspected or non-resaleable.
5. Enter the return reason.
6. To put eligible goods back into sellable stock, explicitly approve **Inspected and approved for resale** and select **Restock**. Only inspected, saleable and unexpired goods qualify, and stock returns to the original batch.
7. Leave restocking off for uninspected or unsuitable goods and segregate them physically.
8. Select **Record return** and review the recorded quantities, disposition and stock handling.

Restocking is not automatic. Do not approve a damaged, expired or otherwise non-saleable medicine just to increase available stock. The recorded inspection identifies the approving operator. A saved non-restocked return is not an automatic promise of a later restocking action.

### Pay and record a refund

1. Review **Refund due** after the return. Return credit first reduces any unpaid invoice amount; only excess money already collected becomes refundable.
2. Pay the customer using the agreed method outside the application.
3. Enter the actual payout method, amount and reference in **Record refund**. Non-cash refunds require a transaction reference.
4. Submit the refund and verify the refund history and remaining refund due.

You may use **Record a confirmed refund with this return** when both the return and an already-confirmed payout should be recorded together. The combined operation saves both records together. It does not initiate a bank transfer.

**Example:** an invoice totals ₹100 and the customer paid ₹100 using ₹40 cash and ₹60 UPI. A ₹25 return creates ₹25 refund due. Paying and recording ₹25 clears that liability. If the customer had paid only ₹40, the same return would reduce the invoice's net value to ₹75, leaving ₹35 outstanding and no refund due.

The original invoice remains available. Use any pending return/refund retry action after an interrupted submission instead of entering the same transaction again. See [returns details](backend/Documents/SALES_RETURNS.md).

## 11. Cancel an incorrectly created sale

**Performed by: Admin or Pharmacist.**

1. Open the incorrect invoice and choose **Cancel this sale**.
2. Enter a clear cancellation reason.
3. Confirm that the medicines never left pharmacy custody. If they were handed to the customer, use returns instead.
4. If restoring stock, select **Restore quantities to the original batches** and explicitly approve inspection of every item. Expired batches are rejected.
5. If stock is not restored, keep the affected physical goods separate from sellable stock.
6. Select **Confirm cancellation**, or **Keep sale** to abandon the cancellation form.
7. Check the cancelled status, reason and stock handling. If payments had been received, arrange the actual refund and have an Admin or Cashier record it.

Cancellation preserves the original invoice and payment history. It does not transfer money and cannot be used on a sale that already has returns or refunds. Cancellation cannot simply be undone; check the invoice carefully before confirming. See [cancellation details](backend/Documents/SALES_CANCELLATION.md).

## 12. Monitor stock, expiry and notifications

1. Open **Inventory** to search by medicine or batch number and check quantities and expiry dates. All three roles can view it.
2. As Admin or Pharmacist, open **Expiry Management** to review expired and near-expiry medicines.
3. Physically separate unsuitable stock and arrange handling under the pharmacy's procedures. Viewing an alert does not dispose of or transfer goods.
4. Open **Notifications**, review new alerts and mark them read after reviewing them.
5. Review low-stock information and arrange purchases for the correct branch.

Alert preferences are configured by the Admin in that branch's Pharmacy Settings. Read status only acknowledges the message; it does not mean the stock issue has been resolved.

## 13. Use the dashboard and export reports

**Performed by: Admin or Pharmacist.**

1. Open **Dashboard** and verify the active branch.
2. Review sales, collections, returns/cancellation credits, refunds, outstanding balances and stock alerts.
3. Choose the required date range for sales analytics. Date-range reports support up to 366 days.
4. In the advanced reporting area, choose the required report: sales, GST, medicine-wise sales, gross profit or current stock valuation.
5. Generate the report, inspect the result and use the Excel or PDF download option.
6. Confirm the branch and period in the downloaded file before sharing it.

**How to read the figures:** sales measure invoices and credits; collections measure payments received. Refunds are money paid out and are not a second reduction of sales. Gross profit excludes operating expenses such as salaries and rent. Current stock valuation is a current snapshot, not a historical stock position for the selected sales dates. Reports are branch-specific.

Admins can use **Audit Logs** to investigate recorded actions. See [advanced reports details](backend/Documents/ADVANCED_REPORTS.md).

## 14. Prepare the monthly GST worksheet

**Performed by: Admin, with accountant review.**

1. Check the active branch, its GSTIN and GST settings.
2. Open **GST Returns**, select the month and choose **Load preparation**.
3. Review the source register of sales, returns/cancellation credits and received purchases, together with validation issues.
4. Add tax details from the actual invoices and issued credit notes: registration and counterparty GSTIN, place of supply, classification and actual tax heads. Purchase stock costs do not automatically establish supplier tax or ITC.
5. If reconciling GSTR-2B, download the CSV headers, prepare the normalized file from the actual statement and use **Replace draft statement**. This replaces the worksheet's previous imported draft statement.
6. Resolve mismatches and select eligible ITC only after accountant review. An exact match alone does not establish legal eligibility.
7. Download draft Excel/PDF worksheets and resolve blocking issues. Do not invent HSN codes or rename issued invoices to clear validation.
8. Confirm review and select **Save reviewed snapshot**. This locks worksheet tax-detail edits and statement replacement.
9. If corrections are needed, enter **Reason to reopen**, select **Reopen worksheet**, correct the data and review again.

Reviewed status is an internal worksheet status. The application does **not** file GST returns, pay tax or generate official GST Portal upload files. Branches sharing a GSTIN require accountant consolidation outside the application. Later source changes can make a review stale and require reopening and reviewing again. Follow the full [GST preparation guide](backend/Documents/GST_RETURN_PREPARATION.md) for CSV requirements and supported scope.

## 15. Daily operating routine

### Before opening

1. Sign in with your own account and verify the branch.
2. Resolve any pending checkout, receipt, return or refund operation before starting a replacement transaction.
3. Review stock availability, near-expiry medicines and outstanding alerts with the responsible staff.
4. Confirm that the printer and any UPI/card collection arrangements are available.

### During business

1. Select the customer, build the cart and verify quantities before issuing the invoice.
2. Collect money and record the actual receipt against that invoice.
3. Give the customer the correct invoice and check the remaining balance.
4. Record returns, stock decisions and actual refunds through their respective workflows.

### At closing

1. Review that day's invoices, recorded receipts, refunds and unpaid balances.
2. Compare recorded Cash receipts/refunds with actual cash and reconcile UPI/Card entries with bank or terminal records.
3. Investigate differences and outstanding refund liabilities rather than entering unsupported balancing transactions.
4. Ask the Admin or Pharmacist to review branch reports and stock/expiry alerts.
5. Export required reports and log out.

This closing routine is a staff reconciliation process. The application does not provide a cash-drawer shift closing or automated bank reconciliation workflow.

## 16. Troubleshooting

For workflow questions, open **Application Help** in the sidebar. Ask a specific question and read the matching user-guide sections. AI generation uses configured Langflow or Ollama services; guide retrieval remains available if generation fails. Application Help cannot inspect live records or perform actions. Do not enter customer information or payment credentials. See [AI setup and scope](backend/Documents/AI_FEATURES.md).

| What you see | What to do |
| --- | --- |
| A menu or button is missing | Check the role table above. Ask the Admin to check your account role and branch assignment. |
| Stock exists elsewhere but is unavailable here | Check Active branch. Branch stock is separate. Do not record another branch's sale in the current branch. |
| A medicine is unavailable for billing | Check whether it is active and has sufficient eligible, unexpired stock in this branch. |
| A purchase draft did not increase stock | Verify the delivery, then use Receive stock. Creating the draft alone does not add stock. |
| New branch cannot bill or has no inventory | Ask the Admin to configure its settings and receive or register legitimate stock. |
| Network error after a financial submission | Check history and use the displayed pending retry action. Do not create a duplicate transaction or clear pending browser data. If the result remains uncertain, ask the Admin to investigate. |
| Payment amount exceeds balance | Refresh the invoice and history; another operator may already have recorded a receipt. |
| UPI QR does not appear or a bank app rejects it | Ask the Admin to check this branch's UPI configuration and bank-supported merchant QR arrangements. Verify actual credit before recording any payment. |
| A return did not increase stock | Check disposition, inspection approval and Restock selection. Non-restocked returns do not enter sellable stock. |
| Cancellation is refused | Check whether goods left custody, previous returns/refunds exist or a batch is expired. Use the appropriate returns workflow or ask the Admin. |
| A refund is recorded but customer has not received money | Recording is not a payout instruction. Investigate the actual bank/cash transaction and reference with the Admin. |
| GST review is blocked or stale | Read the issues, involve the accountant, and reopen/review when appropriate. Do not alter original documents to bypass validation. |
| Sign-in stops working after reset or account changes | Sign in again with the correct credentials. Ask the Admin to check activation and assignment. |
| Persistent server or database error | Give the deployment administrator the page, branch, time, visible message and affected invoice number. Omit passwords and customer payment credentials. |

For support, always identify the branch and original invoice or purchase number. Preserve the original records so the Admin can trace what happened.

## 17. Use AI assistance

1. Open **Application Help** to ask how an application workflow works. Read the source sections and check whether the answer is generated or guide-only. Semantic search is available after the Admin configures and indexes the knowledge service.
2. Open **AI Workspace → Inventory questions** to view branch stock. Set the medicine/batch filter and expiry horizon explicitly; names and dates written only in the question do not filter the results. The assistant classifies stock, expiry or low-stock requests and shows up to 100 matching batches.
3. As Admin or Pharmacist, use **Reorder planner** to review demand-based purchase quantities. Check draft purchases, pack sizes and supplier lead times yourself; nothing is ordered automatically.
4. Use **Sales insights** to compare a generated explanation with the displayed report figures. Invoiced sales and collections measure different events.
5. Use **Feedback sentiment** for anonymized service feedback. Review the classification, summary and themes; it is not a judgement about a patient or employee.
6. In **Purchasing → AI supplier invoice extraction**, paste invoice text or upload a PNG/JPEG image. Correct every extracted value, choose the exact catalog medicine and confirm review before copying values into the unsaved purchase form. Select the supplier and create/receive the purchase through the normal workflow.

Cashiers can use help and inventory questions. Sales insights, reorder planning, invoice extraction and sentiment analysis require Admin or Pharmacist access. Only Admins can build the knowledge index. Features needing an unavailable model show an error or a clearly labelled fallback. No AI feature authorizes stock, financial or clinical actions. For setup and supported formats, see [AI features](backend/Documents/AI_FEATURES.md).
