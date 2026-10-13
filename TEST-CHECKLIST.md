# Quick test order

1. GET /api/health
2. POST /api/categories: {"name":"Footwear","isActive":true}
3. POST /api/categories with parentId equal to Footwear's numeric id: {"name":"Men","parentId":1,"isActive":true}
4. POST /api/categories with parentId equal to Men's numeric id: {"name":"Sneakers","parentId":2,"isActive":true}
5. POST /api/products with categoryId equal to Sneakers' numeric id.
6. GET /api/products?categoryCode=00003 (use actual code from response).
7. Try POST a child under a Level 3 category; expect HTTP 409.
8. Try adding a product to Level 1; expect HTTP 409.
