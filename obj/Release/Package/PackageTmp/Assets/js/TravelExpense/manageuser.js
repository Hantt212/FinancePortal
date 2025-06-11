function togglePassword() {
    if ($('#newIsWindowsAccount').is(':checked')) {
        $('#newPasswordGroup').hide();
    } else {
        $('#newPasswordGroup').show();
    }
}

function resetUserForm() {
    $('#newUserId').val('');
    $('#newUserName').val('');
    $('#newEmployeeCode').val('');
    $('#newUserEmail').val('');
    $('#newIsWindowsAccount').prop('checked', false);
    $('#newPassword').val('');
    $('#newIsActive').prop('checked', true);
    $('.new-role').prop('checked', false);
    $('#saveUserBtn').prop('disabled', true); 
    $('#employeeCodeInfo').addClass('d-none').text('');
    $('#employeeCodeStatus i').removeClass().addClass('fa fa-times text-danger');
    togglePassword();
}

function showToast(message, type = "success") {
    var toastEl = $("#toastMessage");

    let iconColor;
    let toastTitle;

    switch (type) {
        case "success": iconColor = "green"; toastTitle = "Success"; break;
        case "danger": iconColor = "red"; toastTitle = "Error"; break;
        case "info": iconColor = "blue"; toastTitle = "Info"; break;
        case "warning": iconColor = "orange"; toastTitle = "Warning"; break;
        default: iconColor = "gray"; toastTitle = "Notification";
    }

    $("#toastIcon").css("background-color", iconColor);
    $("#toastTitle").text(toastTitle);
    $("#toastTime").text("Just now");
    $("#toastBody").text(message);
    toastEl.toast('show');
}

function loadUserTable() {
    $.get('/Account/GetUserList', function (data) {
        $('#userTable').DataTable({
            data: data,
            bDestroy: true,
            order: [[0, "asc"]],
            dom: 'Bfrtip',
            buttons: ['copy', 'csv', 'excel', 'pdf', 'print'],
            columns: [
                { data: 'UserName' },
                { data: 'EmployeeCode' },
                { data: 'UserEmailAddress' },
                {
                    data: 'IsWindowsAccount',
                    render: function (data) {
                        return data ? 'Yes' : 'No';
                    }
                },
                {
                    data: 'RoleName',
                    render: function (data) {
                        return data || 'N/A';
                    }
                },
                {
                    data: 'IsActive',
                    render: function (data) {
                        return data ? 'Active' : 'Inactive';
                    }
                },
                {
                    data: 'CreatedTime',
                    render: function (data) {
                        return formatJSONDate(data) || 'N/A';
                    }
                },              
                {
                    data: null,
                    orderable: false,
                    render: function (data, type, row) {
                        return `
                            <a class="btn btn-sm btn-outline-primary edit-user" data-id="${row.UserId}">
                                <i class="fa fa-edit"></i> Edit
                            </a>
                        `;
                    }
                }
            ]
        });
    });
}

$(document).on('click', '.edit-user', function () {
    const userId = $(this).data('id');
    isEditMode = true;

    $.get('/Account/GetUserById', { id: userId }, function (res) {
        if (!res.success) {
            showToast(res.message || "Failed to load user.", "danger");
            return;
        }

        const user = res.data;

        // 🔄 Change modal to Edit mode
        $('#userModalTitle').text("Edit User");
        $('#newUserId').val(user.UserId);
        $('#newEmployeeCode').val(user.EmployeeCode);
        $('#newUserName').val(user.UserName);
        $('#newUserEmail').val(user.UserEmailAddress);
        $('#newIsWindowsAccount').prop('checked', user.IsWindowsAccount);
        $('#newIsActive').prop('checked', user.IsActive);

        // ✅ Show valid icon + text
        $('#employeeCodeStatus i').removeClass().addClass('fa fa-check text-success');
        $('#employeeCodeInfo').removeClass('d-none').text(`${user.EmployeeCode} is valid`);

        // ✅ Pre-select roles
        $('#newRoles').val(user.SelectedRoleIds.map(String)).trigger('change');

        // Enable save
        $('#saveUserBtn').prop('disabled', false).html('Update');

        togglePassword();
        // 🔁 Show modal
        $('#addUserModal').modal('show');
    });
});

function formatJSONDate(jsonDate) {
    if (!jsonDate) return "N/A";

    let timestamp = parseInt(jsonDate.replace(/[^0-9]/g, ""), 10);
    let date = new Date(timestamp);

    let day = String(date.getDate()).padStart(2, "0");
    let month = String(date.getMonth() + 1).padStart(2, "0"); // Months are 0-based
    let year = date.getFullYear();
    let hours = String(date.getHours()).padStart(2, "0");
    let minutes = String(date.getMinutes()).padStart(2, "0");

    return `${year}-${month}-${day} ${hours}:${minutes}`;
}

$('#newEmployeeCode').on('input', function () {
    const code = $(this).val().trim();

    if (!code) {
        $('#employeeCodeStatus i').removeClass().addClass('fa fa-times text-danger');
        $('#employeeCodeInfo').addClass('d-none').text('');
        return;
    }

    $.get('/TravelExpense/GetEmployeeByCode', { code }, function (res) {
        if (res.success) {
            $('#employeeCodeStatus i').removeClass().addClass('fa fa-check text-success');
            $('#employeeCodeInfo').removeClass('d-none').text(`${res.data.Name} - ${res.data.Position}`);

            // Prefill email + username
            $('#newUserEmail').val(res.data.Email);
            if (res.data.Email && res.data.Email.includes('@')) {
                const username = res.data.Email.split('@')[0];
                $('#newUserName').val(username);
            }
            $('#saveUserBtn').prop('disabled', false);
        } else {
            $('#employeeCodeStatus i').removeClass().addClass('fa fa-times text-danger');
            $('#employeeCodeInfo').removeClass('d-none').text('Employee not found');

            // Clear dependent fields
            $('#newUserEmail, #newUserName').val('');
            $('#saveUserBtn').prop('disabled', true);
        }
    });
});

let isEditMode = false;

$(document).ready(function () {

    togglePassword();
    loadUserTable();

    $('#newIsWindowsAccount').change(togglePassword);

    // ✅ Handle Add New User
    $('#addUserBtn').click(function () {
        isEditMode = false;
        resetUserForm();
        $('#userModalTitle').text("Add New User");
        $('#newIsWindowsAccount').prop('checked', true);
        togglePassword();
        $('#saveUserBtn').html('Save');
        $('#addUserModal').modal('show');
    });

    // ✅ Save User
    $('#saveUserBtn').click(function () {
        const payload = {
            UserId: parseInt($('#newUserId').val()) || 0,
            UserName: $('#newUserName').val().trim(),
            EmployeeCode: $('#newEmployeeCode').val().trim(),
            UserEmailAddress: $('#newUserEmail').val().trim(),
            IsWindowsAccount: $('#newIsWindowsAccount').is(':checked'),
            Password: $('#newPassword').val().trim(),
            IsActive: $('#newIsActive').is(':checked'),
            SelectedRoleIds: $('#newRoles').val()
        };

        // ✅ Basic validation
        if (!payload.EmployeeCode) {
            showToast("Employee Code is required.", "warning");
            return;
        }

        if (!payload.UserName) {
            showToast("Username is required.", "warning");
            return;
        }

        if (!payload.UserEmailAddress || !payload.UserEmailAddress.includes('@')) {
            showToast("Valid Email is required.", "warning");
            return;
        }

        const btn = $(this);
        const originalText = isEditMode ? 'Update' : 'Save';
        btn.prop('disabled', true).html(`<i class="fa fa-spinner fa-spin"></i> ${isEditMode ? 'Updating' : 'Saving'}...`);

        $.ajax({
            url: '/Account/SaveUser',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (res) {
                if (res.success) {
                    const successMsg = isEditMode ? "User updated successfully!" : "User saved successfully!";
                    showToast(successMsg, "success");
                    $('#addUserModal').modal('hide');
                    loadUserTable();
                } else {
                    switch (res.message) {
                        case "duplicate_username":
                            showToast("Username already exists.", "danger");
                            break;
                        case "duplicate_employee":
                            showToast("Employee code already exists.", "danger");
                            break;
                        default:
                            showToast(res.message || "Failed to save user.", "danger");
                    }
                }
                // 🔁 Reset button label appropriately
                const fallbackText = isEditMode ? 'Update' : 'Save';
                btn.prop('disabled', false).html(fallbackText);
            },
            error: function () {
                showToast("Server error occurred.", "danger");
                const fallbackText = isEditMode ? 'Update' : 'Save';
                btn.prop('disabled', false).html(fallbackText);
            }
        });
    });

    $('#addUserModal').on('show.bs.modal', function () {
        if (!isEditMode) {
            $('#userModalTitle').text("Add New User");
        } else {
            $('#userModalTitle').text("Edit User");
        }

        // Always set the button label based on mode
        $('#saveUserBtn').html(isEditMode ? 'Update' : 'Save');
    });


    $('#addUserModal').on('hidden.bs.modal', function () {
        $('#saveUserBtn').prop('disabled', false).html(isEditMode ? 'Update' : 'Save');
        resetUserForm();
    });
});

