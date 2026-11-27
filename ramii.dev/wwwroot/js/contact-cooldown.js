export function getCooldown() {
    const prefix = "ramii_contact_cooldown=";

    const cookie = document.cookie
        .split(";")
        .map(value => value.trim())
        .find(value => value.startsWith(prefix));

    return cookie
        ? decodeURIComponent(cookie.substring(prefix.length))
        : null;
}