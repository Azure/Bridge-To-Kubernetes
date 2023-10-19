ARG ARCH=amd64
FROM mcr.microsoft.com/cbl-mariner/base/nodejs:18.17.1-2-cm2.0.20231004-${ARCH}

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

CMD ["npm", "start"]