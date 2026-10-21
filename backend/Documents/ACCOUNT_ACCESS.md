# Employee access and password recovery

Sign in as Admin and open **Employee Management** (`/users`). Create an employee
with their name, email, initial password, role (Pharmacist or Cashier) and assigned
branch. On first sign-in, the employee must replace their temporary password.

If an employee forgets their password:

1. Admin opens Employee Management and selects **Reset password** on that employee.
2. Verify the employee's name and email, enter and confirm a temporary password,
   then submit the reset form.
3. Share the temporary password privately with that employee. Passwords are not
   included in success messages or audit entries.
4. The old password and existing access tokens no longer work. The employee must
   sign in with the temporary password and choose a new password before working.

Resetting a password does not activate an inactive employee account. Passwords
require at least 12 characters, uppercase, lowercase, a number and a special
character. Password reset, mandatory-change flag and audit entry commit in one
transaction; an audit failure rolls everything back.

All signed-in roles can use **Change password** in the sidebar account area.
This requires their current password and signs them out after a successful change
when they select Back to sign in. A regular change can be cancelled with Back to
workspace; a mandatory initial/reset password change cannot be skipped.

Employee reset is Admin-only and cannot reset another Admin. Email/SMS recovery
and forgotten-Admin recovery are not implemented. The login page directs employees
to their administrator; it does not claim to send a reset email.
