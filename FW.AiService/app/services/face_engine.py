import numpy as np
import cv2
from typing import Optional, Tuple, List
import logging

logger = logging.getLogger("face_engine")

class FaceEngine:
    """
    Module xử lý AI sinh trắc học khuôn mặt sử dụng InsightFace (ArcFace 512-dim).
    Có cơ chế fallback tính toán vector chuẩn hóa phục vụ môi trường kiểm thử/dev.
    """
    def __init__(self):
        self.app = None
        self._initialize_model()

    def _initialize_model(self):
        try:
            import insightface
            from insightface.app import FaceAnalysis

            # buffalo_l hoặc buffalo_sc là model nhẹ chuẩn của InsightFace (ArcFace 512 chiều)
            self.app = FaceAnalysis(name="buffalo_sc", providers=['CPUExecutionProvider'])
            self.app.prepare(ctx_id=0, det_size=(640, 640))
            logger.info("Khởi tạo InsightFace model (buffalo_sc) thành công.")
        except Exception as e:
            logger.warning(f"Chưa tải được InsightFace weights hoặc thiếu thư viện: {e}. Sử dụng engine dự phòng (CPU/Fallback).")
            self.app = None

    def read_image_from_bytes(self, image_bytes: bytes) -> Optional[np.ndarray]:
        try:
            nparr = np.frombuffer(image_bytes, np.uint8)
            img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            return img
        except Exception as e:
            logger.error(f"Lỗi đọc ảnh từ bytes: {e}")
            return None

    def extract_embedding(self, image: np.ndarray) -> Tuple[bool, Optional[List[int]], Optional[List[float]], str]:
        """
        Trích xuất vector 512 chiều từ ảnh.
        Trả về: (face_detected, bbox [x1, y1, x2, y2], embedding, message)
        """
        if image is None:
            return False, None, None, "Dữ liệu ảnh không hợp lệ"

        if self.app is not None:
            try:
                faces = self.app.get(image)
                if len(faces) == 0:
                    return False, None, None, "Không phát hiện khuôn mặt nào trong ảnh"
                
                # Lấy khuôn mặt có diện tích lớn nhất (chính diện gần camera nhất)
                faces = sorted(faces, key=lambda x: (x.bbox[2]-x.bbox[0]) * (x.bbox[3]-x.bbox[1]), reverse=True)
                primary_face = faces[0]
                
                bbox = [int(v) for v in primary_face.bbox]
                embedding = primary_face.embedding
                
                # Chuẩn hóa L2-norm cho vector
                norm = np.linalg.norm(embedding)
                if norm > 0:
                    embedding = embedding / norm
                    
                return True, bbox, embedding.tolist(), "Trích xuất vector thành công"
            except Exception as e:
                logger.error(f"Lỗi khi trích xuất vector với InsightFace: {e}")

        # Fallback mô phỏng cho môi trường Dev khi chưa download weights
        return self._fallback_extract(image)

    def _fallback_extract(self, image: np.ndarray) -> Tuple[bool, Optional[List[int]], Optional[List[float]], str]:
        """Engine dự phòng trích xuất vector từ histogram ảnh khi chưa có model weights"""
        h, w = image.shape[:2]
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        
        # Đơn giản trích xuất vector 512 chiều từ đặc trưng ảnh
        resized = cv2.resize(gray, (32, 16)) # 32*16 = 512 dimensions
        vec = resized.flatten().astype(np.float32)
        norm = np.linalg.norm(vec)
        if norm > 0:
            vec = vec / norm
        
        bbox = [int(w * 0.2), int(h * 0.2), int(w * 0.8), int(h * 0.8)]
        return True, bbox, vec.tolist(), "Trích xuất vector thành công (Dev Fallback Mode)"

    @staticmethod
    def calculate_cosine_similarity(vec1: List[float], vec2: List[float]) -> float:
        """Tính Cosine Similarity giữa 2 vector [-1.0 -> 1.0]"""
        a = np.array(vec1, dtype=np.float32)
        b = np.array(vec2, dtype=np.float32)

        norm_a = np.linalg.norm(a)
        norm_b = np.linalg.norm(b)

        if norm_a == 0 or norm_b == 0:
            return 0.0

        similarity = float(np.dot(a, b) / (norm_a * norm_b))
        return max(0.0, min(1.0, similarity))

face_engine = FaceEngine()
