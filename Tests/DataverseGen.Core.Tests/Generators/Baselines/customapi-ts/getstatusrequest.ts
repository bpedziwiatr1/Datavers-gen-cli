export default class GetStatusRequest implements IWebApiRequest {
	private operationName: string;
	private customerid: string;

	constructor(private customerid: string) {
		this.operationName = "newgetstatus";
		this.customerid = customerid;
	}
	getInternalRequest(): IWebApiInternalRequest {
		return {
			customerid: this.customerid,
			getMetadata: () => ({
				boundParameter: null,
				parameterTypes: {
					customerid: {
						typeName: "Edm.String",
						structuralProperty: WebApiRequestStructuralProperty.PrimitiveType
					},
				},
				operationType: WebApiRequestOperationType.Function,
				operationName: this.operationName
			})
		} as IWebApiInternalRequest;
	}
}
