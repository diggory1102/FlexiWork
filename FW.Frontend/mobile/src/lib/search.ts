/** Chuẩn hóa để tìm kiếm: chữ thường, bỏ dấu tiếng Việt (đ → d), gộp khoảng trắng. */
export const normalizeSearch = (text: string) =>
  text
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/\s+/g, ' ')
    .trim();

/** Từ khóa (gõ có dấu hay không dấu) có nằm trong một trong các trường không. Từ khóa trống khớp tất cả. */
export const matchesSearch = (query: string, ...fields: string[]) => {
  const wanted = normalizeSearch(query);
  return !wanted || fields.some((field) => normalizeSearch(field).includes(wanted));
};
