/**
 * Kiểm tra tính hợp lệ của dữ liệu biểu mẫu phía Client trước khi gọi REST API.
 */

export function isValidEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email || '').trim());
}

export function isValidStudentCode(code) {
    return /^[A-Za-z0-9_-]{3,20}$/.test(String(code || '').trim());
}

export function isValidPassword(password) {
    return String(password || '').length >= 6;
}

export function validatePdfFile(file, maxSizeMb = 25) {
    if (!file) {
        return { valid: false, message: 'Vui lòng chọn một tệp PDF học liệu.' };
    }
    const isPdf = file.name.toLowerCase().endsWith('.pdf') || file.type === 'application/pdf';
    if (!isPdf) {
        return { valid: false, message: 'Chỉ chấp nhận tệp định dạng .pdf hợp lệ.' };
    }
    if (file.size <= 0 || file.size > maxSizeMb * 1024 * 1024) {
        return { valid: false, message: `Dung lượng tệp PDF không được vượt quá ${maxSizeMb} MB.` };
    }
    return { valid: true, message: '' };
}

export function validateAvatarFile(file, maxSizeMb = 5) {
    if (!file) {
        return { valid: false, message: 'Vui lòng chọn một tệp ảnh.' };
    }
    const allowedExt = ['.jpg', '.jpeg', '.png', '.webp'];
    const lowerName = file.name.toLowerCase();
    const ok = allowedExt.some((ext) => lowerName.endsWith(ext));
    if (!ok) {
        return { valid: false, message: 'Ảnh đại diện chỉ hỗ trợ định dạng JPG, PNG hoặc WEBP.' };
    }
    if (file.size > maxSizeMb * 1024 * 1024) {
        return { valid: false, message: `Dung lượng ảnh không được vượt quá ${maxSizeMb} MB.` };
    }
    return { valid: true, message: '' };
}
